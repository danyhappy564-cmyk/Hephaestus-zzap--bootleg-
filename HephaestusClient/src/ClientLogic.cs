#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HephaestusClient
{
    /// <summary>
    /// Pure logic of the F12 plugin (no BepInEx / Unity): language choice, currency names, request/response JSON.
    /// Also compiled into the test harness.
    /// </summary>
    public static class ClientLogic
    {
        /// <summary>Server mod routes (must match LiveSettingsRouter on the server).</summary>
        public const string GetRoute = "/hephaestus/settings/get";
        public const string SetRoute = "/hephaestus/settings/set";

        public const string LangAuto = "자동 · Auto";
        public const string LangKorean = "한국어 · Korean";
        public const string LangEnglish = "English";

        public const string Euros = "569668774bdc2da2298b4568";
        public const string Roubles = "5449016a4bdc2d6f028b456f";
        public const string Dollars = "5696686a4bdc2da3298b456a";

        public const string CurEuro = "유로 · EUR";
        public const string CurRouble = "루블 · RUB";
        public const string CurDollar = "달러 · USD";

        public static readonly string[] LanguageValues = { LangAuto, LangKorean, LangEnglish };
        public static readonly string[] CurrencyValues = { CurEuro, CurRouble, CurDollar };

        /// <summary>
        /// Korean or English for the F12 menu.
        /// Auto: the language the game has applied ("kr" = Korean, anything else = English);
        /// while the game hasn't applied one yet, the Windows display language.
        /// </summary>
        public static bool UseKorean(string mode, string gameCulture, bool windowsKorean)
        {
            if (mode == LangKorean)
            {
                return true;
            }

            if (mode == LangEnglish)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(gameCulture))
            {
                return gameCulture == "kr";
            }

            return windowsKorean;
        }

        public static string CurrencyIdFromValue(string value)
        {
            switch (value)
            {
                case CurRouble: return Roubles;
                case CurDollar: return Dollars;
                default: return Euros;
            }
        }

        public static string CurrencyValueFromId(string id)
        {
            switch (id)
            {
                case Roubles: return CurRouble;
                case Dollars: return CurDollar;
                default: return CurEuro;
            }
        }

        public static string BuildSetRequest(string currencyId, int discount, int cost, bool debugLogging)
        {
            return "{\"currency\":\"" + currencyId + "\""
                   + ",\"discount\":" + discount.ToString(CultureInfo.InvariantCulture)
                   + ",\"cost\":" + cost.ToString(CultureInfo.InvariantCulture)
                   + ",\"debugLogging\":" + (debugLogging ? "true" : "false")
                   + "}";
        }

        /// <summary>Server answer of /hephaestus/settings/get|set.</summary>
        public sealed class Response
        {
            public bool Ok;
            public string MessageKo = "";
            public string MessageEn = "";
            public bool HasSettings;
            public string Currency = Euros;
            public double Discount;
            public double Cost;
            public bool DebugLogging;
        }

        public static Response ParseResponse(string json)
        {
            var root = MiniJson.Parse(json) as Dictionary<string, object>;
            if (root == null)
            {
                throw new FormatException("response is not a JSON object");
            }

            var r = new Response
            {
                Ok = root.TryGetValue("ok", out var ok) && ok is bool b && b,
                MessageKo = root.TryGetValue("messageKo", out var mk) ? mk as string ?? "" : "",
                MessageEn = root.TryGetValue("messageEn", out var me) ? me as string ?? "" : "",
            };

            if (root.TryGetValue("settings", out var s) && s is Dictionary<string, object> settings)
            {
                r.HasSettings = true;
                if (settings.TryGetValue("currency", out var c) && c is string cs) r.Currency = cs;
                if (settings.TryGetValue("discount", out var d) && d is double dd) r.Discount = dd;
                if (settings.TryGetValue("cost", out var co) && co is double cod) r.Cost = cod;
                if (settings.TryGetValue("debugLogging", out var dl) && dl is bool dlb) r.DebugLogging = dlb;
            }

            return r;
        }
    }

    /// <summary>
    /// Tiny JSON reader (objects, arrays, strings with escapes, numbers, true/false/null) so the plugin needs no
    /// JSON library. Objects become Dictionary&lt;string, object&gt;, numbers double, arrays List&lt;object&gt;.
    /// </summary>
    public static class MiniJson
    {
        private const int MaxDepth = 32;

        public static object Parse(string json)
        {
            if (json == null)
            {
                throw new FormatException("null input");
            }

            int i = 0;
            var value = ReadValue(json, ref i, 0);
            SkipWs(json, ref i);
            if (i != json.Length)
            {
                throw new FormatException("trailing characters at " + i);
            }
            return value;
        }

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        private static object ReadValue(string s, ref int i, int depth)
        {
            if (depth > MaxDepth) throw new FormatException("too deep");
            SkipWs(s, ref i);
            if (i >= s.Length) throw new FormatException("unexpected end");

            char c = s[i];
            if (c == '{') return ReadObject(s, ref i, depth);
            if (c == '[') return ReadArray(s, ref i, depth);
            if (c == '"') return ReadString(s, ref i);
            if (Match(s, ref i, "true")) return true;
            if (Match(s, ref i, "false")) return false;
            if (Match(s, ref i, "null")) return null;
            return ReadNumber(s, ref i);
        }

        private static bool Match(string s, ref int i, string word)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) == 0)
            {
                i += word.Length;
                return true;
            }
            return false;
        }

        private static Dictionary<string, object> ReadObject(string s, ref int i, int depth)
        {
            var result = new Dictionary<string, object>();
            i++; // {
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return result; }

            while (true)
            {
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != '"') throw new FormatException("expected key at " + i);
                var key = ReadString(s, ref i);
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new FormatException("expected ':' at " + i);
                i++;
                result[key] = ReadValue(s, ref i, depth + 1);
                SkipWs(s, ref i);
                if (i >= s.Length) throw new FormatException("unexpected end in object");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return result; }
                throw new FormatException("expected ',' or '}' at " + i);
            }
        }

        private static List<object> ReadArray(string s, ref int i, int depth)
        {
            var result = new List<object>();
            i++; // [
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return result; }

            while (true)
            {
                result.Add(ReadValue(s, ref i, depth + 1));
                SkipWs(s, ref i);
                if (i >= s.Length) throw new FormatException("unexpected end in array");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return result; }
                throw new FormatException("expected ',' or ']' at " + i);
            }
        }

        private static string ReadString(string s, ref int i)
        {
            i++; // opening quote
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw new FormatException("bad \\u escape");
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: throw new FormatException("bad escape \\" + e);
                }
            }
            throw new FormatException("unterminated string");
        }

        private static double ReadNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (i == start) throw new FormatException("unexpected character '" + s[i] + "' at " + i);
            return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }
}
