using System.Text.Json.Serialization;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;

namespace Hephaestus;

/// <summary>Settings the F12 menu (client plugin) can read and change. Null = leave as is.</summary>
public record LiveSettings : IRequestData
{
    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("discount")]
    public double? Discount { get; set; }

    [JsonPropertyName("cost")]
    public double? Cost { get; set; }

    [JsonPropertyName("debugLogging")]
    public bool? DebugLogging { get; set; }
}

public record LiveSettingsResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    /// <summary>Korean and English so the client can show the one matching its language.</summary>
    [JsonPropertyName("messageKo")]
    public string MessageKo { get; set; } = "";

    [JsonPropertyName("messageEn")]
    public string MessageEn { get; set; } = "";

    [JsonPropertyName("settings")]
    public LiveSettings? Settings { get; set; }
}

/// <summary>
/// Holds the current config, saves F12 changes to config/config.json and hands the new values to
/// <see cref="BuildAssortService"/> (prices change the next time the trader screen is opened).
/// </summary>
[Injectable(InjectionType.Singleton)]
public class LiveSettingsService(ISptLogger<LiveSettingsService> logger, BuildAssortService buildAssortService)
{
    private readonly object _lock = new();
    private string? _path;

    public HephaestusConfig Current { get; private set; } = new();

    /// <summary>Called once by the loader.</summary>
    public void Init(string configPath, HephaestusConfig config)
    {
        lock (_lock)
        {
            _path = configPath;
            Current = config;
            buildAssortService.Configure(config);
        }
    }

    public LiveSettingsResponse Get()
    {
        lock (_lock)
        {
            return new LiveSettingsResponse { Ok = true, Settings = ToDto(Current) };
        }
    }

    public LiveSettingsResponse Set(LiveSettings? request)
    {
        lock (_lock)
        {
            if (request is null)
            {
                return new LiveSettingsResponse
                {
                    Ok = false,
                    MessageKo = "요청이 비어 있습니다",
                    MessageEn = "empty request",
                    Settings = ToDto(Current),
                };
            }

            var next = Current.With(request.Currency, request.Discount, request.Cost, request.DebugLogging);
            Current = next;
            buildAssortService.Configure(next);

            string ko = "저장했습니다. 상인 화면을 다시 열면 새 가격이 나옵니다.";
            string en = "Saved. Reopen the trader screen to see the new prices.";
            var ok = true;
            try
            {
                if (_path is not null)
                {
                    next.Save(_path);
                }
            }
            catch (Exception ex)
            {
                ok = false;
                ko = $"적용은 했지만 config.json 저장에 실패했습니다({ex.Message}). 서버를 다시 켜면 예전 값으로 돌아갑니다.";
                en = $"Applied, but saving config.json failed ({ex.Message}). The old values return after a server restart.";
                logger.Warning($"[Hephaestus] {Loc.L(ko, en)}");
            }

            if (next.Warnings.Count > 0)
            {
                ko += " (" + string.Join(", ", next.Warnings) + ")";
                en += " (" + string.Join(", ", next.Warnings) + ")";
            }

            logger.Info(Loc.L(
                $"[Hephaestus] F12에서 설정 변경: 화폐 {CurrencyName(next.Currency)}, 할인 {next.Discount}%, 대체 가격 {next.Cost}, 디버그 로그 {(next.DebugLogging ? "켬" : "끔")}",
                $"[Hephaestus] settings changed from F12: currency {CurrencyName(next.Currency)}, discount {next.Discount}%, fallback cost {next.Cost}, debug log {(next.DebugLogging ? "on" : "off")}"));

            return new LiveSettingsResponse { Ok = ok, MessageKo = ko, MessageEn = en, Settings = ToDto(next) };
        }
    }

    public static LiveSettings ToDto(HephaestusConfig c) => new()
    {
        Currency = c.Currency,
        Discount = c.Discount,
        Cost = c.Cost,
        DebugLogging = c.DebugLogging,
    };

    public static string CurrencyName(string tpl) => tpl switch
    {
        HephaestusConfig.Roubles => "RUB",
        HephaestusConfig.Dollars => "USD",
        _ => "EUR",
    };
}

/// <summary>Routes used by the client plugin's F12 menu. Plain JSON (the client sends requestcompressed: 0).</summary>
[Injectable]
public class LiveSettingsRouter(JsonUtil jsonUtil, LiveSettingsService service) : StaticRouter(
    jsonUtil,
    [
        new RouteAction<EmptyRequestData>(
            GetRoute,
            (url, info, sessionId, output, cancellationToken) =>
                ValueTask.FromResult(jsonUtil.Serialize(service.Get())!)
        ),
        new RouteAction<LiveSettings>(
            SetRoute,
            (url, info, sessionId, output, cancellationToken) =>
                ValueTask.FromResult(jsonUtil.Serialize(service.Set(info))!)
        ),
    ]
)
{
    public const string GetRoute = "/hephaestus/settings/get";
    public const string SetRoute = "/hephaestus/settings/set";
}
