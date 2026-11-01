using System.Reflection;
using Path = System.IO.Path;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Routers;

namespace Hephaestus;

/// <summary>
/// Registers the Hephaestus trader (base data, names, portrait, refresh time) and turns on the assort hook.
/// Runs at Preload: the database is fully loaded then and the trader exists before profiles and
/// trader callbacks run.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.Preload + 1)]
public class HephaestusLoader(
    ISptLogger<HephaestusLoader> logger,
    ModHelper modHelper,
    ImageRouter imageRouter,
    TraderConfig traderConfig,
    TradersTable tradersTable,
    LocaleTable localeTable,
    BuildAssortService buildAssortService,
    LiveSettingsService liveSettings) : IOnLoad
{
    private const int RefreshSeconds = 3600;

    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());

        var configPath = Path.Combine(modPath, "config", "config.json");
        var config = HephaestusConfig.Load(configPath);
        foreach (var warning in config.Warnings)
        {
            logger.Warning($"[Hephaestus] {warning}");
        }
        liveSettings.Init(configPath, config);

        var traderBase = modHelper.GetJsonDataFromFile<TraderBase>(Path.Combine(modPath, "db"), "base.json");
        if (traderBase is null || traderBase.Id != BuildAssortService.TraderId)
        {
            logger.Error(Loc.L(
                "[Hephaestus] db/base.json이 없거나 _id가 달라서 상인을 추가하지 않았습니다",
                "[Hephaestus] db/base.json is missing or has a different _id, trader not added"));
            return Task.CompletedTask;
        }

        RegisterAvatar(modPath, traderBase);
        RegisterRefreshTime(traderBase);

        tradersTable[traderBase.Id] = new Trader
        {
            Base = traderBase,
            Assort = new TraderAssort
            {
                Items = [],
                BarterScheme = [],
                LoyalLevelItems = [],
            },
            // Keys must be lower case, the server reads them literally at startup.
            QuestAssort = new Dictionary<string, Dictionary<MongoId, MongoId>>
            {
                ["started"] = [],
                ["success"] = [],
                ["fail"] = [],
            },
            Dialogue = [],
            Suits = [],
            Services = [],
        };

        AddLocales(traderBase);

        GetAssortPatch.Service = buildAssortService;
        new GetAssortPatch().Enable();

        logger.Success(Loc.L(
            $"[Hephaestus] 상인 추가 완료 (화폐 {LiveSettingsService.CurrencyName(config.Currency)}, 할인 {config.Discount}%)",
            $"[Hephaestus] trader added (currency {LiveSettingsService.CurrencyName(config.Currency)}, discount {config.Discount}%)"));
        return Task.CompletedTask;
    }

    private void RegisterAvatar(string modPath, TraderBase traderBase)
    {
        var image = Path.Combine(modPath, "res", "img.jpg");
        var avatar = traderBase.Avatar ?? "/files/trader/avatar/hephaestus_alxk.jpg";
        if (!File.Exists(image))
        {
            logger.Warning(Loc.L($"[Hephaestus] 초상화 파일이 없습니다: {image}", $"[Hephaestus] portrait not found at {image}"));
            return;
        }

        // ImageRouter keys are the URL without the extension.
        imageRouter.AddRoute(avatar.Replace(".jpg", "", StringComparison.OrdinalIgnoreCase), image);
    }

    private void RegisterRefreshTime(TraderBase traderBase)
    {
        traderConfig.UpdateTime.RemoveAll(u => u.TraderId == traderBase.Id);
        traderConfig.UpdateTime.Add(new UpdateTime
        {
            Name = "Hephaestus",
            TraderId = traderBase.Id,
            Seconds = new MinMax<int>(RefreshSeconds, RefreshSeconds),
        });
    }

    /// <summary>
    /// Name / description per game language: the game asks the server for the locale the player picked
    /// in the game settings, so Korean players get "kr" (Korean) and everyone else English.
    /// </summary>
    private void AddLocales(TraderBase traderBase)
    {
        var id = traderBase.Id.ToString();

        foreach (var (code, locale) in localeTable.Global)
        {
            var texts = Loc.TraderTextsFor(code);
            locale.AddTransformer(dict =>
            {
                if (dict is null)
                {
                    return dict;
                }

                WriteTraderTexts(dict, id, texts);
                return dict;
            });
        }
    }

    internal static void WriteTraderTexts(IDictionary<string, string> dict, string traderId, TraderTexts texts)
    {
        dict[$"{traderId} FullName"] = texts.FullName;
        dict[$"{traderId} FirstName"] = texts.FirstName;
        dict[$"{traderId} Nickname"] = texts.Nickname;
        dict[$"{traderId} Location"] = texts.Location;
        dict[$"{traderId} Description"] = texts.Description;
    }
}
