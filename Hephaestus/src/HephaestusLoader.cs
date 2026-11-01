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
    BuildAssortService buildAssortService) : IOnLoad
{
    private const int RefreshSeconds = 3600;

    private const string Description =
        "You share the reseller license of your creations to Hephaestus and in return you get a hefty discount.";

    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());

        var config = HephaestusConfig.Load(Path.Combine(modPath, "config", "config.json"));
        foreach (var warning in config.Warnings)
        {
            logger.Warning($"[Hephaestus] {warning}");
        }
        buildAssortService.Configure(config);

        var traderBase = modHelper.GetJsonDataFromFile<TraderBase>(Path.Combine(modPath, "db"), "base.json");
        if (traderBase is null || traderBase.Id != BuildAssortService.TraderId)
        {
            logger.Error("[Hephaestus] db/base.json is missing or has a different _id, trader not added");
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

        logger.Success($"[Hephaestus] trader added (currency {CurrencyName(config.Currency)}, discount {config.Discount}%)");
        return Task.CompletedTask;
    }

    private void RegisterAvatar(string modPath, TraderBase traderBase)
    {
        var image = Path.Combine(modPath, "res", "img.jpg");
        var avatar = traderBase.Avatar ?? "/files/trader/avatar/hephaestus_alxk.jpg";
        if (!File.Exists(image))
        {
            logger.Warning($"[Hephaestus] portrait not found at {image}");
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

    private void AddLocales(TraderBase traderBase)
    {
        var id = traderBase.Id.ToString();
        var fullName = traderBase.Name ?? "Hephaestus";
        var nickname = traderBase.Nickname ?? "Hephaestus";
        var location = traderBase.Location ?? "";

        foreach (var (_, locale) in localeTable.Global)
        {
            locale.AddTransformer(dict =>
            {
                if (dict is null)
                {
                    return dict;
                }

                dict[$"{id} FullName"] = fullName;
                dict[$"{id} FirstName"] = "Hephaestus";
                dict[$"{id} Nickname"] = nickname;
                dict[$"{id} Location"] = location;
                dict[$"{id} Description"] = Description;
                return dict;
            });
        }
    }

    private static string CurrencyName(string tpl) => tpl switch
    {
        HephaestusConfig.Roubles => "RUB",
        HephaestusConfig.Dollars => "USD",
        _ => "EUR",
    };
}
