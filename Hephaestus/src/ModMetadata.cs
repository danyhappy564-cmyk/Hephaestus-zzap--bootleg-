using SPTarkov.Server.Core.Models.Spt.Mod;
using SemVerRange = SemanticVersioning.Range;
using SemVerVersion = SemanticVersioning.Version;

namespace Hephaestus;

/// <summary>
/// Mod information shown by the SPT 4.1 server mod loader.
/// </summary>
public class ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.alexkarpen.hephaestus";
    public string Name { get; init; } = "Hephaestus";
    public string Author { get; init; } = "alexkarpen";
    public List<string>? Contributors { get; init; } = ["R_F (SPT 4.1 port)"];
    public SemVerVersion Version { get; init; } = new("1.2.0");
    public SemVerRange SptVersion { get; init; } = new("~4.1.6");
    public bool HasPrepatcher { get; init; }
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, SemVerRange>? ModDependencies { get; init; }
    public string? Url { get; init; } = "https://github.com/alexkarpen/Hephaestus";
    public string License { get; init; } = "MIT";
}
