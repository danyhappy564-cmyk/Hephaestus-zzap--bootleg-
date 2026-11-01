using System.Security.Cryptography;
using System.Text;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Ragfair;
using SPTarkov.Server.Core.Utils.Cloners;

namespace Hephaestus;

/// <summary>
/// Turns a player's saved weapon builds (weapon presets) into Hephaestus' assort.
///
/// Differences from the original 3.7 TS code (the "problem" parts):
/// - The original overwrote the first item of every saved build inside the player's profile
///   (parentId/upd/BackgroundColor), so the profile's own presets were modified and saved to disk.
///   Here the build is cloned first and the profile is never touched.
/// - The original replaced the whole trader assort with whichever profile logged in last, so with
///   several profiles (Fika) players saw and bought each other's builds. Here every profile keeps
///   its own part and <see cref="FilterForSession"/> only returns that player's builds.
/// - The original priced with the randomised flea offer price on every inventory move. Here the
///   price is the plain (non-random) flea price of every part, so the shown price stays the same.
/// - Builds that contain an item no longer in the database (removed mod) are skipped instead of
///   breaking the trader screen; prices are whole numbers and never 0.
/// - Assort item ids are derived from the build, so they never equal the ids of real items
///   in the stash (a saved build can share ids with the weapon it was saved from).
/// </summary>
[Injectable(InjectionType.Singleton)]
public class BuildAssortService(
    ISptLogger<BuildAssortService> logger,
    TradersTable tradersTable,
    TemplateTable templateTable,
    ProfileHelper profileHelper,
    ItemHelper itemHelper,
    HandbookHelper handbookHelper,
    RagfairPriceService ragfairPriceService,
    ICloner cloner)
{
    public const string TraderIdString = "a18160fc82b4f2be55205c89";
    public static readonly MongoId TraderId = new(TraderIdString);

    internal const int StockPerOffer = 2000;

    /// <summary>Held from the GetAssort prefix until its finalizer (same thread), see <see cref="GetAssortPatch"/>.</summary>
    internal readonly object Gate = new();

    /// <summary>One profile's offers.</summary>
    internal sealed class SessionPart
    {
        public List<Item> Items { get; } = [];
        public Dictionary<MongoId, List<List<BarterScheme>>> Barter { get; } = [];
        public HashSet<MongoId> ItemIds { get; } = [];
        public HashSet<MongoId> RootIds { get; } = [];
    }

    private readonly Dictionary<MongoId, SessionPart> _parts = [];

    private HephaestusConfig _config = new();

    public void Configure(HephaestusConfig config) => _config = config;

    /// <summary>
    /// Rebuild this session's offers and write the union of every session's offers into the trader table
    /// (the purchase code looks the bought offer up in the raw table, so it has to be there).
    /// Caller holds <see cref="Gate"/>.
    /// </summary>
    public void RebuildForSession(MongoId sessionId)
    {
        if (!tradersTable.TryGetValue(TraderId, out var trader) || trader is null)
        {
            return;
        }

        var part = new SessionPart();
        int added = 0, skipped = 0;
        var currency = new MongoId(_config.Currency);

        var builds = profileHelper.GetFullProfile(sessionId)?.UserBuildData?.WeaponBuilds;
        foreach (var build in builds ?? [])
        {
            try
            {
                var ok = TryAddBuild(part, build.Id, build.Root, build.Items, currency,
                    tpl => templateTable.Items.ContainsKey(tpl),
                    list => cloner.Clone(list)!,
                    GetPrice);
                if (ok) added++; else skipped++;
            }
            catch (Exception ex)
            {
                skipped++;
                logger.Warning(Loc.L($"[Hephaestus] 프리셋 \"{build.Name}\" 건너뜀: {ex.Message}", $"[Hephaestus] build \"{build.Name}\" skipped: {ex.Message}"));
            }
        }

        _parts[sessionId] = part;
        trader.Assort = Compose(_parts.Values, trader.Assort?.NextResupply);

        if (_config.DebugLogging)
        {
            logger.Info(Loc.L(
                $"[Hephaestus] 세션 {sessionId}: 프리셋 {added}개 판매, {skipped}개 건너뜀, 상인 물품 전체 {trader.Assort.Items.Count}개",
                $"[Hephaestus] session {sessionId}: {added} build(s) for sale, {skipped} skipped, {trader.Assort.Items.Count} assort items in total"));
        }
    }

    /// <summary>Remove offers that belong to other sessions from the assort sent to this session.</summary>
    public void FilterForSession(MongoId sessionId, TraderAssort? result)
    {
        _parts.TryGetValue(sessionId, out var part);
        Filter(part, result);
    }

    // ---- pure logic below (no server state; covered by the test harness) ----

    /// <summary>Union of every profile's offers, as written into the trader table.</summary>
    internal static TraderAssort Compose(IEnumerable<SessionPart> parts, double? nextResupply)
    {
        var assort = new TraderAssort
        {
            NextResupply = nextResupply,
            Items = [],
            BarterScheme = [],
            LoyalLevelItems = [],
        };

        foreach (var p in parts)
        {
            assort.Items.AddRange(p.Items);
            foreach (var (rootId, scheme) in p.Barter)
            {
                assort.BarterScheme[rootId] = scheme;
                assort.LoyalLevelItems[rootId] = 1;
            }
        }

        return assort;
    }

    /// <summary>Keep only <paramref name="part"/>'s offers in <paramref name="result"/> (nothing when part is null).</summary>
    internal static void Filter(SessionPart? part, TraderAssort? result)
    {
        if (result is null)
        {
            return;
        }

        if (part is null)
        {
            result.Items?.Clear();
            result.BarterScheme?.Clear();
            result.LoyalLevelItems?.Clear();
            return;
        }

        result.Items?.RemoveAll(i => !part.ItemIds.Contains(i.Id));

        if (result.BarterScheme is not null)
        {
            foreach (var key in result.BarterScheme.Keys.Where(k => !part.RootIds.Contains(k)).ToList())
            {
                result.BarterScheme.Remove(key);
            }
        }

        if (result.LoyalLevelItems is not null)
        {
            foreach (var key in result.LoyalLevelItems.Keys.Where(k => !part.RootIds.Contains(k)).ToList())
            {
                result.LoyalLevelItems.Remove(key);
            }
        }
    }

    /// <summary>
    /// Add one saved build to <paramref name="part"/> as an offer. Returns false when the build is skipped.
    /// <paramref name="buildItems"/> is never modified.
    /// </summary>
    internal static bool TryAddBuild(
        SessionPart part,
        MongoId buildId,
        MongoId rootId,
        List<Item>? buildItems,
        MongoId currency,
        Func<MongoId, bool> templateExists,
        Func<List<Item>, List<Item>> clone,
        Func<List<Item>, double> price)
    {
        if (buildItems is null || buildItems.Count == 0)
        {
            return false;
        }

        // Root = the item the build says is the root; old/odd builds fall back to the first item like the original.
        var root = buildItems.FirstOrDefault(i => i.Id == rootId) ?? buildItems[0];

        // Only the root and items really attached to it (a broken parent chain would leave orphans).
        var tree = buildItems.GetItemWithChildren(root.Id);
        if (tree.Count == 0)
        {
            return false;
        }

        // A part from a removed mod would make the offer unbuyable / break the trader screen.
        if (tree.Any(i => !templateExists(i.Template)))
        {
            return false;
        }

        // Clone: never modify the profile's saved build (the original did).
        var items = clone(tree);

        var idMap = new Dictionary<string, MongoId>(items.Count);
        foreach (var item in items)
        {
            idMap[item.Id.ToString()] = DeriveId(buildId, item.Id);
        }

        foreach (var item in items)
        {
            item.Id = idMap[item.Id.ToString()];
            if (item.ParentId is not null && idMap.TryGetValue(item.ParentId, out var newParent))
            {
                item.ParentId = newParent.ToString();
            }
        }

        var offerRoot = items[0];
        if (part.RootIds.Contains(offerRoot.Id))
        {
            return false; // same build listed twice
        }

        offerRoot.ParentId = "hideout";
        offerRoot.SlotId = "hideout";
        offerRoot.Location = null;
        offerRoot.Upd = new Upd
        {
            UnlimitedCount = true,
            StackObjectsCount = StockPerOffer,
        };

        var count = price(tree);

        part.RootIds.Add(offerRoot.Id);
        part.Items.AddRange(items);
        foreach (var item in items)
        {
            part.ItemIds.Add(item.Id);
        }

        part.Barter[offerRoot.Id] =
        [
            [
                new BarterScheme
                {
                    Count = count,
                    Template = currency,
                },
            ],
        ];

        return true;
    }

    /// <summary>
    /// Final price from the summed rouble flea price: converted to the configured currency, minus the discount,
    /// rounded, at least 1. Falls back to config "cost" (already in the configured currency) when no price is known.
    /// </summary>
    internal static double FinalPrice(double roubles, HephaestusConfig config, Func<double, double> fromRoubles)
    {
        double price = roubles > 0
            ? (config.Currency == HephaestusConfig.Roubles ? roubles : fromRoubles(roubles))
            : config.Cost;

        price *= 1 - (config.Discount / 100.0);

        return Math.Max(1, Math.Round(price));
    }

    /// <summary>Stable 24-hex id per (build, item) so offers keep their id between rebuilds.</summary>
    internal static MongoId DeriveId(MongoId buildId, MongoId itemId)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes($"hephaestus:{buildId}:{itemId}"));
        return new MongoId(Convert.ToHexStringLower(hash.AsSpan(0, 12)));
    }

    /// <summary>Flea price of every part (no random offer multiplier), see <see cref="FinalPrice"/>.</summary>
    private double GetPrice(List<Item> buildItems)
    {
        double roubles = 0;
        foreach (var item in buildItems)
        {
            if (itemHelper.IsOfBaseclass(item.Template, BaseClasses.BUILT_IN_INSERTS))
            {
                continue;
            }

            roubles += ragfairPriceService.GetDynamicPriceForItem(item.Template)
                       ?? ragfairPriceService.GetStaticPriceForItem(item.Template)
                       ?? 0;
        }

        var currency = new MongoId(_config.Currency);
        return FinalPrice(roubles, _config, r => handbookHelper.FromRoubles(r, currency));
    }
}
