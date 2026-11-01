using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using BepInEx;
using BepInEx.Configuration;

namespace HephaestusClient
{
    /// <summary>
    /// F12 menu for the Hephaestus server mod: trader price settings (saved to the server's config.json)
    /// and Korean / English text chosen automatically from the game language (or Windows language).
    /// The server mod works without this plugin; this only adds the F12 menu.
    /// </summary>
    [BepInPlugin(Guid, "Hephaestus F12", Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.alexkarpen.hephaestus.f12";
        public const string Version = "1.2.0";

        private const string SecLanguage = "0. Language";
        private const string SecPrices = "1. Trader prices";
        private const string SecStatus = "2. Status";

        private const double SendDelaySeconds = 0.8;
        private const double LanguageCheckSeconds = 1.0;
        private const double FetchRetrySeconds = 10.0;
        private const int MaxFetchAttempts = 6;
        private const int MaxCost = 10_000_000;

        private ConfigEntry<string> _language;
        private ConfigEntry<int> _discount;
        private ConfigEntry<string> _currency;
        private ConfigEntry<int> _cost;
        private ConfigEntry<bool> _debugLog;
        private ConfigEntry<string> _status;

        /// <summary>Attributes per "section|key", rewritten on language change.</summary>
        private readonly Dictionary<string, ConfigurationManagerAttributes> _attributes = new Dictionary<string, ConfigurationManagerAttributes>();

        private readonly ConcurrentQueue<Action> _mainThread = new ConcurrentQueue<Action>();
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        private bool _suppress;
        private bool? _korean;
        private double _nextLanguageCheck;
        private double _sendAt = -1;
        private int _requestsInFlight;
        private int _fetchAttempts;
        private double _nextFetch;
        private bool _fetched;

        private string _lastMessageKo = "아직 서버에 연결하지 않았습니다";
        private string _lastMessageEn = "Not connected to the server yet";

        private double Now => _clock.Elapsed.TotalSeconds;

        private bool Korean => _korean ?? false;

        private string L(string korean, string english) => Korean ? korean : english;

        private void Awake()
        {
            _language = Bind(SecLanguage, "Language", ClientLogic.LangAuto, 100,
                new AcceptableValueList<string>(ClientLogic.LanguageValues));

            _discount = Bind(SecPrices, "Discount (%)", 90, 90, new AcceptableValueRange<int>(0, 100));
            _currency = Bind(SecPrices, "Currency", ClientLogic.CurEuro, 80,
                new AcceptableValueList<string>(ClientLogic.CurrencyValues));
            _cost = Bind(SecPrices, "Fallback cost", 123, 70, null);
            _debugLog = Bind(SecPrices, "Debug log", false, 60, null);

            _status = Bind(SecStatus, "Server", "", 50, null);
            var statusAttr = _attributes[SecStatus + "|Server"];
            statusAttr.ReadOnly = true;
            statusAttr.HideDefaultButton = true;

            _language.SettingChanged += (s, e) => UpdateLanguage(force: true);
            _discount.SettingChanged += (s, e) => OnPriceSettingChanged();
            _currency.SettingChanged += (s, e) => OnPriceSettingChanged();
            _cost.SettingChanged += (s, e) => OnPriceSettingChanged();
            _debugLog.SettingChanged += (s, e) => OnPriceSettingChanged();

            UpdateLanguage(force: true);
            Logger.LogInfo($"Hephaestus F12 {Version} loaded (game language: {LanguageDetector.GameCulture() ?? "not set yet"}, Windows Korean: {LanguageDetector.WindowsKorean()})");
        }

        private void Update()
        {
            // results of background requests, applied on the main thread (bounded per frame)
            for (int i = 0; i < 16 && _mainThread.TryDequeue(out var action); i++)
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Logger.LogWarning("main thread action failed: " + ex.Message);
                }
            }

            var now = Now;

            if (now >= _nextLanguageCheck)
            {
                _nextLanguageCheck = now + LanguageCheckSeconds;
                UpdateLanguage(force: false);
            }

            if (!_fetched && _fetchAttempts < MaxFetchAttempts && now >= _nextFetch && _requestsInFlight == 0)
            {
                _fetchAttempts++;
                _nextFetch = now + FetchRetrySeconds;
                Send(ServerApi.GetRoute, "{}", isFetch: true);
            }

            if (_sendAt >= 0 && now >= _sendAt && _requestsInFlight == 0)
            {
                _sendAt = -1;
                var body = ClientLogic.BuildSetRequest(
                    ClientLogic.CurrencyIdFromValue(_currency.Value),
                    Clamp(_discount.Value, 0, 100),
                    Clamp(_cost.Value, 1, MaxCost),
                    _debugLog.Value);
                Send(ServerApi.SetRoute, body, isFetch: false);
            }
        }

        private void OnPriceSettingChanged()
        {
            if (_suppress)
            {
                return;
            }

            // several quick changes (slider drag) become one request
            _sendAt = Now + SendDelaySeconds;
        }

        private void Send(string route, string body, bool isFetch)
        {
            Interlocked.Increment(ref _requestsInFlight);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var json = ServerApi.Post(route, body);
                    var response = ClientLogic.ParseResponse(json);
                    _mainThread.Enqueue(() => ApplyResponse(response, isFetch));
                }
                catch (Exception ex)
                {
                    var message = ex.Message;
                    _mainThread.Enqueue(() => ApplyFailure(message, isFetch));
                }
                finally
                {
                    Interlocked.Decrement(ref _requestsInFlight);
                }
            });
        }

        private void ApplyResponse(ClientLogic.Response response, bool isFetch)
        {
            if (isFetch)
            {
                _fetched = true;
            }

            // a change the player made while the first fetch was running wins over the server's old values
            if (response.HasSettings && !(isFetch && _sendAt >= 0))
            {
                _suppress = true;
                try
                {
                    SetIfDifferent(_discount, Clamp((int)Math.Round(response.Discount), 0, 100));
                    SetIfDifferent(_currency, ClientLogic.CurrencyValueFromId(response.Currency));
                    SetIfDifferent(_cost, Clamp((int)Math.Round(response.Cost), 1, MaxCost));
                    SetIfDifferent(_debugLog, response.DebugLogging);
                }
                finally
                {
                    _suppress = false;
                }
            }

            if (isFetch)
            {
                _lastMessageKo = response.Ok ? "연결됨 — 서버 설정을 불러왔습니다" : "서버가 설정을 주지 않았습니다";
                _lastMessageEn = response.Ok ? "Connected — loaded the server settings" : "The server did not return settings";
            }
            else
            {
                _lastMessageKo = response.MessageKo;
                _lastMessageEn = response.MessageEn;
                Logger.LogInfo("server: " + response.MessageEn);
            }

            ShowStatus();
        }

        private void ApplyFailure(string message, bool isFetch)
        {
            _lastMessageKo = (isFetch ? "서버에서 설정을 못 받았습니다: " : "서버에 저장하지 못했습니다: ") + message
                             + " (서버 모드 Hephaestus가 4.1 버전인지 확인)";
            _lastMessageEn = (isFetch ? "Could not load settings from the server: " : "Could not save to the server: ") + message
                             + " (check the Hephaestus server mod is the 4.1 version)";
            Logger.LogWarning(_lastMessageEn);
            ShowStatus();
        }

        private void ShowStatus()
        {
            _suppress = true;
            try
            {
                SetIfDifferent(_status, L(_lastMessageKo, _lastMessageEn));
            }
            finally
            {
                _suppress = false;
            }
        }

        /// <summary>Re-check the language; on change rewrite every F12 name/description (shown after reopening F12).</summary>
        private void UpdateLanguage(bool force)
        {
            var korean = ClientLogic.UseKorean(_language.Value, LanguageDetector.GameCulture(), LanguageDetector.WindowsKorean());
            if (!force && _korean == korean)
            {
                return;
            }

            var changed = _korean != korean;
            _korean = korean;

            foreach (var pair in _attributes)
            {
                if (!Texts.TryGetValue(pair.Key, out var t))
                {
                    continue;
                }

                var section = pair.Key.Substring(0, pair.Key.IndexOf('|'));
                Categories.TryGetValue(section, out var cat);
                pair.Value.Category = cat == null ? section : (korean ? cat[0] : cat[1]);
                pair.Value.DispName = korean ? t[0] : t[2];
                pair.Value.Description = korean ? t[1] : t[3];
            }

            ShowStatus();
            if (changed)
            {
                Logger.LogInfo("F12 language: " + (korean ? "Korean" : "English"));
            }
        }

        private ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, int order, AcceptableValueBase acceptable)
        {
            var attr = new ConfigurationManagerAttributes { Order = order };
            _attributes[section + "|" + key] = attr;
            Texts.TryGetValue(section + "|" + key, out var t);
            var description = t == null ? key : t[3];
            return Config.Bind(section, key, defaultValue, new ConfigDescription(description, acceptable, attr));
        }

        private static void SetIfDifferent<T>(ConfigEntry<T> entry, T value)
        {
            if (!EqualityComparer<T>.Default.Equals(entry.Value, value))
            {
                entry.Value = value;
            }
        }

        private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;

        /// <summary>Section → [Korean, English] category names.</summary>
        private static readonly Dictionary<string, string[]> Categories = new Dictionary<string, string[]>
        {
            [SecLanguage] = new[] { "0. 언어", "0. Language" },
            [SecPrices] = new[] { "1. 상인 가격", "1. Trader prices" },
            [SecStatus] = new[] { "2. 상태", "2. Status" },
        };

        /// <summary>"section|key" → [Korean name, Korean description, English name, English description].</summary>
        private static readonly Dictionary<string, string[]> Texts = new Dictionary<string, string[]>
        {
            [SecLanguage + "|Language"] = new[]
            {
                "언어",
                "F12 화면의 언어입니다. 자동 = 게임 설정 언어를 따르고(한국어면 한국어, 그 밖은 영어), 게임이 아직 언어를 정하기 전에는 윈도우 표시 언어를 씁니다. " +
                "상인 이름·설명은 이 설정과 상관없이 게임 언어로 나옵니다. 바꾼 뒤 F12 창을 닫았다 다시 열면 적용됩니다.",
                "Language",
                "Language of this F12 menu. Auto = follows the game's language setting (Korean → Korean, anything else → English); " +
                "before the game has applied its language, the Windows display language is used. The trader's name and description always follow the game language. " +
                "Close and reopen F12 to see the change.",
            },
            [SecPrices + "|Discount (%)"] = new[]
            {
                "할인율 (%)",
                "프리셋 부품들의 플리마켓 시세 합에서 깎아 주는 비율입니다. 0 = 시세 그대로, 90 = 시세의 10%, 100 = 사실상 공짜(최소 1). " +
                "서버의 config.json에 저장되고, 상인 화면을 다시 열면 새 가격이 나옵니다.",
                "Discount (%)",
                "How much is taken off the summed flea price of the build's parts. 0 = full flea price, 90 = 10% of it, 100 = practically free (at least 1). " +
                "Saved to the server's config.json; reopen the trader screen to see the new prices.",
            },
            [SecPrices + "|Currency"] = new[]
            {
                "가격 화폐",
                "프리셋을 살 때 내는 돈의 종류입니다. 시세(루블)를 이 화폐로 바꿔 계산합니다.",
                "Currency",
                "Money the builds are sold for. The flea price (roubles) is converted to this currency.",
            },
            [SecPrices + "|Fallback cost"] = new[]
            {
                "대체 가격",
                "프리셋 부품 시세를 하나도 구하지 못했을 때 쓰는 가격입니다(위 화폐 기준, 할인율도 적용). 1~10,000,000.",
                "Fallback cost",
                "Price used when no flea price is known for any part of a build (in the currency above, discount applied too). 1-10,000,000.",
            },
            [SecPrices + "|Debug log"] = new[]
            {
                "서버 디버그 로그",
                "켜면 상인 목록을 만들 때마다 서버 로그에 \"프리셋 몇 개 판매 / 몇 개 건너뜀\" 한 줄을 남깁니다. 문제 제보용입니다.",
                "Server debug log",
                "When on, the server log gets one line per trader refresh (\"N builds for sale / M skipped\"). For bug reports.",
            },
            [SecStatus + "|Server"] = new[]
            {
                "서버 연결 상태",
                "서버 모드와의 마지막 통신 결과입니다(읽기 전용).",
                "Server connection",
                "Result of the last exchange with the server mod (read only).",
            },
        };
    }
}
