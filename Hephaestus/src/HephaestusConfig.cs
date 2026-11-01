using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hephaestus;

/// <summary>
/// config/config.json. Read leniently like the original TS mod did
/// (numbers may be written as strings, unknown "HELP_*" keys are ignored).
/// Also written back when the F12 menu (client plugin) changes a value.
/// </summary>
public sealed class HephaestusConfig
{
    public const string Euros = "569668774bdc2da2298b4568";
    public const string Roubles = "5449016a4bdc2d6f028b456f";
    public const string Dollars = "5696686a4bdc2da3298b456a";

    public const double MinCost = 1;
    public const double MaxCost = 10_000_000;

    /// <summary>Fallback price (in <see cref="Currency"/>) when a build's flea price can't be calculated.</summary>
    public double Cost { get; private set; } = 123;

    /// <summary>Currency template id used for the price.</summary>
    public string Currency { get; private set; } = Euros;

    /// <summary>0-100 percent discount on the flea price.</summary>
    public double Discount { get; private set; } = 90;

    public bool DebugLogging { get; private set; }

    /// <summary>Problems found while reading (shown once as warnings).</summary>
    public List<string> Warnings { get; } = [];

    public static HephaestusConfig Load(string path)
    {
        var cfg = new HephaestusConfig();
        if (!File.Exists(path))
        {
            cfg.Warnings.Add(Loc.L($"설정 파일이 없어 기본값을 씁니다: {path}", $"config not found at {path}, using defaults"));
            return cfg;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
            var root = doc.RootElement;

            cfg.Apply(
                TryNumber(root, "cost", out var cost) ? cost : null,
                root.TryGetProperty("currency", out var cur) && cur.ValueKind == JsonValueKind.String ? cur.GetString() : null,
                TryNumber(root, "discount", out var discount) ? discount : null,
                root.TryGetProperty("debugLogging", out var dbg) && dbg.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? dbg.GetBoolean()
                    : null);
        }
        catch (Exception ex)
        {
            cfg.Warnings.Add(Loc.L($"설정 파일을 읽지 못해 기본값을 씁니다({ex.Message})", $"config could not be read ({ex.Message}), using defaults"));
        }

        return cfg;
    }

    /// <summary>A copy of this config with the given values changed (null = keep). Invalid values are fixed and noted in Warnings.</summary>
    public HephaestusConfig With(string? currency, double? discount, double? cost, bool? debugLogging)
    {
        var copy = new HephaestusConfig
        {
            Cost = Cost,
            Currency = Currency,
            Discount = Discount,
            DebugLogging = DebugLogging,
        };
        copy.Apply(cost, currency, discount, debugLogging);
        return copy;
    }

    private void Apply(double? cost, string? currency, double? discount, bool? debugLogging)
    {
        if (cost is not null)
        {
            if (double.IsNaN(cost.Value) || cost < MinCost || cost > MaxCost)
            {
                Warnings.Add(Loc.L($"대체 가격 {cost}은(는) {MinCost}~{MaxCost} 범위로 맞춥니다", $"cost {cost} is outside {MinCost}-{MaxCost}, clamped"));
            }
            Cost = double.IsNaN(cost.Value) ? Cost : Math.Clamp(cost.Value, MinCost, MaxCost);
        }

        if (currency is not null)
        {
            if (currency is Euros or Roubles or Dollars)
            {
                Currency = currency;
            }
            else
            {
                Warnings.Add(Loc.L($"알 수 없는 화폐 \"{currency}\" — 유로로 씁니다", $"unknown currency \"{currency}\", using euros"));
                Currency = Euros;
            }
        }

        if (discount is not null)
        {
            if (double.IsNaN(discount.Value) || discount is < 0 or > 100)
            {
                Warnings.Add(Loc.L($"할인율 {discount}은(는) 0~100 범위로 맞춥니다", $"discount {discount} is outside 0-100, clamped"));
            }
            Discount = double.IsNaN(discount.Value) ? Discount : Math.Clamp(discount.Value, 0, 100);
        }

        if (debugLogging is not null)
        {
            DebugLogging = debugLogging.Value;
        }
    }

    /// <summary>
    /// Write the four values into config.json, keeping every other key (HELP_* notes) as it is.
    /// Written to a temp file first so a crash can't leave half a file.
    /// </summary>
    public void Save(string path)
    {
        JsonObject root;
        try
        {
            root = File.Exists(path)
                ? JsonNode.Parse(File.ReadAllText(path), documentOptions: new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                }) as JsonObject ?? new JsonObject()
                : new JsonObject();
        }
        catch
        {
            root = new JsonObject();
        }

        root["cost"] = Cost;
        root["currency"] = Currency;
        root["discount"] = Discount;
        root["debugLogging"] = DebugLogging;

        var json = root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var tmp = path + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, path, overwrite: true);
    }

    private static bool TryNumber(JsonElement root, string name, out double value)
    {
        value = 0;
        if (!root.TryGetProperty(name, out var el))
        {
            return false;
        }

        return el.ValueKind switch
        {
            JsonValueKind.Number => el.TryGetDouble(out value),
            JsonValueKind.String => double.TryParse(el.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value),
            _ => false,
        };
    }
}
