using System.Globalization;
using System.Text.Json;

namespace Hephaestus;

/// <summary>
/// config/config.json. Read leniently like the original TS mod did
/// (numbers may be written as strings, unknown "HELP_*" keys are ignored).
/// </summary>
public sealed class HephaestusConfig
{
    public const string Euros = "569668774bdc2da2298b4568";
    public const string Roubles = "5449016a4bdc2d6f028b456f";
    public const string Dollars = "5696686a4bdc2da3298b456a";

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
            cfg.Warnings.Add($"config not found at {path}, using defaults");
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

            if (TryNumber(root, "cost", out var cost) && cost > 0)
            {
                cfg.Cost = cost;
            }

            if (root.TryGetProperty("currency", out var cur) && cur.ValueKind == JsonValueKind.String)
            {
                var id = cur.GetString() ?? "";
                if (id is Euros or Roubles or Dollars)
                {
                    cfg.Currency = id;
                }
                else
                {
                    cfg.Warnings.Add($"unknown currency \"{id}\", using euros");
                }
            }

            if (TryNumber(root, "discount", out var discount))
            {
                if (discount is < 0 or > 100)
                {
                    cfg.Warnings.Add($"discount {discount} is outside 0-100, clamped");
                }
                cfg.Discount = Math.Clamp(discount, 0, 100);
            }

            if (root.TryGetProperty("debugLogging", out var dbg) &&
                dbg.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                cfg.DebugLogging = dbg.GetBoolean();
            }
        }
        catch (Exception ex)
        {
            cfg.Warnings.Add($"config could not be read ({ex.Message}), using defaults");
        }

        return cfg;
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
