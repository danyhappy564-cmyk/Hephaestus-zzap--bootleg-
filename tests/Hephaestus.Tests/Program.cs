// Test harness for the Hephaestus 4.1 port.
// Runs the mod's own logic against real SPT 4.1.6 model types (no server needed).
// Usage: dotnet run --project tests/Hephaestus.Tests -- <path to Hephaestus mod folder>

using System.Reflection;
using Path = System.IO.Path;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hephaestus;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Helpers.Traders;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Json;

var modDir = args.Length > 0 ? args[0] : Path.Combine("..", "..", "Hephaestus");
int pass = 0, fail = 0;

void Check(bool ok, string name)
{
    if (ok) { pass++; Console.WriteLine($"PASS {name}"); }
    else { fail++; Console.WriteLine($"FAIL {name}"); }
}

var jsonUtil = new JsonUtil([new SptJsonConverterRegistrator()]);
List<Item> Clone(List<Item> items) => jsonUtil.Deserialize<List<Item>>(jsonUtil.Serialize(items))!;
string Dump(object o) => jsonUtil.Serialize(o)!;

// ---------- 1. db/base.json through SPT's own JSON settings ----------
var traderBase = jsonUtil.DeserializeFromFile<TraderBase>(Path.Combine(modDir, "db", "base.json"));
Check(traderBase is not null, "base.json deserializes as TraderBase");
Check(traderBase!.Id == BuildAssortService.TraderId, "base.json _id == trader id used in code");
Check(traderBase.IsAvailableInPVE, "isAvailableInPVE true (trader visible in PVE)");
Check(traderBase.Avatar == "/files/trader/avatar/hephaestus_alxk.jpg", "unique avatar path");
Check(Math.Abs((traderBase.Repair?.Quality ?? 0) - 0.01) < 1e-9, "repair quality read as number");
Check(traderBase.LoyaltyLevels?.Count == 1 && traderBase.LoyaltyLevels[0].MinLevel == 1, "one loyalty level");
Check(traderBase.Currency?.ToString() == "EUR", "trader currency EUR");
Check(traderBase.UnlockedByDefault == true, "unlocked by default");
Check(File.Exists(Path.Combine(modDir, "res", "img.jpg")), "portrait res/img.jpg present");

// ---------- 2. config ----------
var cfg = HephaestusConfig.Load(Path.Combine(modDir, "config", "config.json"));
Check(cfg.Warnings.Count == 0 && cfg.Cost == 123 && cfg.Currency == HephaestusConfig.Euros && cfg.Discount == 90 && !cfg.DebugLogging,
    "shipped config: cost 123, EUR, discount 90, debug off, no warnings");

var tmp = Path.Combine(Path.GetTempPath(), "heph_cfg_test.json");
File.WriteAllText(tmp, "{ \"cost\": \"200\", \"currency\": \"abc\", \"discount\": \"150\", // comment\n }");
var cfg2 = HephaestusConfig.Load(tmp);
Check(cfg2.Cost == 200 && cfg2.Currency == HephaestusConfig.Euros && cfg2.Discount == 100 && cfg2.Warnings.Count == 2,
    "lenient config: number strings, bad currency -> EUR, discount clamped, 2 warnings");
File.WriteAllText(tmp, "{ not json");
var cfg3 = HephaestusConfig.Load(tmp);
Check(cfg3.Warnings.Count == 1 && cfg3.Discount == 90, "broken config -> defaults + warning");
var cfg4 = HephaestusConfig.Load(Path.Combine(Path.GetTempPath(), "does_not_exist.json"));
Check(cfg4.Warnings.Count == 1 && cfg4.Cost == 123, "missing config -> defaults + warning");
File.WriteAllText(tmp, """{ "currency": "5449016a4bdc2d6f028b456f", "discount": 0, "debugLogging": true }""");
var cfgRub = HephaestusConfig.Load(tmp);
Check(cfgRub.Currency == HephaestusConfig.Roubles && cfgRub.Discount == 0 && cfgRub.DebugLogging, "roubles / no discount / debug on");

// ---------- 3. real weapon data: the original mod's dumped assort (db/test.json in the 3.7 mod) ----------
var testJson = Path.Combine(modDir, "..", "user", "mods", "Hephaestus", "db", "test.json");
var raw = JsonNode.Parse(File.ReadAllText(testJson))!.AsArray();
var valid = new JsonArray();
foreach (var n in raw)
{
    var id = n!["_id"]!.GetValue<string>();
    var parent = n["parentId"]?.GetValue<string>();
    var tpl = n["_tpl"]!.GetValue<string>();
    if (MongoId.IsValidMongoId(id) && MongoId.IsValidMongoId(tpl) && (parent is null or "hideout" || MongoId.IsValidMongoId(parent)))
    {
        valid.Add(n.DeepClone());
    }
}
var allItems = jsonUtil.Deserialize<List<Item>>(valid.ToJsonString())!;
var roots = allItems.Where(i => i.ParentId == "hideout").ToList();
Console.WriteLine($"     test data: {allItems.Count} items, {roots.Count} weapons");
Check(roots.Count >= 3, "test data has several weapons");

// Turn each dumped weapon back into a "saved build" like the profile stores it (root has no hideout fields).
var builds = new List<(MongoId BuildId, MongoId Root, List<Item> Items)>();
var rnd = new Random(1);
foreach (var r in roots)
{
    var tree = SPTarkov.Server.Core.Extensions.ItemExtensions.GetItemWithChildren(allItems, r.Id);
    var items = Clone(tree);
    items[0].ParentId = null;
    items[0].SlotId = null;
    items[0].Upd = null;
    // put the root somewhere else in the list, the code must find it by build.Root
    if (items.Count > 2)
    {
        var first = items[0]; items.RemoveAt(0); items.Insert(1, first);
    }
    builds.Add((new MongoId(), r.Id, items));
}

var eur = new MongoId(HephaestusConfig.Euros);
var partA = new BuildAssortService.SessionPart();
var before = builds.Select(b => Dump(b.Items)).ToList();
int added = 0;
foreach (var b in builds)
{
    if (BuildAssortService.TryAddBuild(partA, b.BuildId, b.Root, b.Items, eur, _ => true, Clone, l => l.Count * 10)) added++;
}
Check(added == builds.Count, "every build becomes an offer");
Check(builds.Select(b => Dump(b.Items)).SequenceEqual(before), "saved builds in the profile are not modified");

var inputIds = builds.SelectMany(b => b.Items).Select(i => i.Id).ToHashSet();
Check(partA.Items.All(i => !inputIds.Contains(i.Id)), "offer item ids differ from the build's (stash) ids");
Check(partA.Items.Select(i => i.Id).Distinct().Count() == partA.Items.Count, "offer item ids unique");
Check(partA.Items.Count == builds.Sum(b => b.Items.Count), "all parts copied");

var outIds = partA.Items.Select(i => i.Id.ToString()).ToHashSet();
var offerRoots = partA.Items.Where(i => i.ParentId == "hideout").ToList();
Check(offerRoots.Count == builds.Count, "one hideout root per offer");
Check(partA.Items.Where(i => i.ParentId != "hideout").All(i => outIds.Contains(i.ParentId!)), "every part's parent exists in the offer");
Check(offerRoots.All(r => r.SlotId == "hideout" && r.Upd?.UnlimitedCount == true && r.Upd?.StackObjectsCount == 2000 && r.Location is null),
    "root: slot hideout, unlimited, 2000 stock");
Check(offerRoots.Select(r => r.Template).ToHashSet().SetEquals(builds.Select(b => b.Items.First(i => i.Id == b.Root).Template)),
    "root templates = build weapons");
Check(partA.Barter.Count == builds.Count && partA.RootIds.SetEquals(partA.Barter.Keys), "one barter scheme per offer");
var firstScheme = partA.Barter[offerRoots[0].Id][0][0];
Check(firstScheme.Template == eur && firstScheme.Count == partA.Items.GetItemWithChildrenCount(offerRoots[0].Id) * 10,
    "barter: configured currency, price from price function");

// parts ordering: GetItemWithChildren should put the root first so items[0] is the root
foreach (var r in offerRoots)
{
    var idx = partA.Items.IndexOf(r);
    Check(idx >= 0, $"offer root {r.Template} present");
    break;
}

// deterministic ids
var partA2 = new BuildAssortService.SessionPart();
foreach (var b in builds) BuildAssortService.TryAddBuild(partA2, b.BuildId, b.Root, b.Items, eur, _ => true, Clone, _ => 1);
Check(partA2.Items.Select(i => i.Id).SequenceEqual(partA.Items.Select(i => i.Id)), "rebuild gives the same offer ids");

// duplicate build in same part
Check(!BuildAssortService.TryAddBuild(partA2, builds[0].BuildId, builds[0].Root, builds[0].Items, eur, _ => true, Clone, _ => 1),
    "same build twice is skipped");

// unknown template (removed mod)
var missingTpl = builds[0].Items.Last().Template;
var partM = new BuildAssortService.SessionPart();
Check(!BuildAssortService.TryAddBuild(partM, builds[0].BuildId, builds[0].Root, builds[0].Items, eur, t => t != missingTpl, Clone, _ => 1)
      && partM.Items.Count == 0, "build with a part missing from the database is skipped");

// orphan part (parent not in build) is not copied
var withOrphan = Clone(builds[1].Items);
withOrphan.Add(new Item { Id = new MongoId(), Template = withOrphan[0].Template, ParentId = new MongoId().ToString(), SlotId = "mod_x" });
var partO = new BuildAssortService.SessionPart();
BuildAssortService.TryAddBuild(partO, builds[1].BuildId, builds[1].Root, withOrphan, eur, _ => true, Clone, _ => 1);
Check(partO.Items.Count == builds[1].Items.Count, "orphan part dropped");

// root id not found -> first item (original behaviour)
var partF = new BuildAssortService.SessionPart();
var single = Clone(builds[2].Items.Where(i => i.Id == builds[2].Root).ToList());
Check(BuildAssortService.TryAddBuild(partF, new MongoId(), new MongoId(), single, eur, _ => true, Clone, _ => 1) && partF.Items.Count == 1,
    "unknown root id falls back to first item");

Check(!BuildAssortService.TryAddBuild(new(), new MongoId(), new MongoId(), [], eur, _ => true, Clone, _ => 1)
      && !BuildAssortService.TryAddBuild(new(), new MongoId(), new MongoId(), null, eur, _ => true, Clone, _ => 1),
    "empty / null build skipped");

// ---------- 4. per-profile separation ----------
var partB = new BuildAssortService.SessionPart();
BuildAssortService.TryAddBuild(partB, new MongoId(), builds[0].Root, builds[0].Items, eur, _ => true, Clone, _ => 5);
var union = BuildAssortService.Compose([partA, partB], 12345);
Check(union.Items.Count == partA.Items.Count + partB.Items.Count && union.BarterScheme.Count == partA.Barter.Count + partB.Barter.Count
      && union.LoyalLevelItems.Count == union.BarterScheme.Count && union.LoyalLevelItems.Values.All(v => v == 1) && union.NextResupply == 12345,
    "table assort = union of all profiles, loyalty 1");

var forA = jsonUtil.Deserialize<TraderAssort>(jsonUtil.Serialize(union))!;
BuildAssortService.Filter(partA, forA);
Check(forA.Items.Count == partA.Items.Count && forA.BarterScheme.Keys.ToHashSet().SetEquals(partA.RootIds)
      && forA.LoyalLevelItems.Keys.ToHashSet().SetEquals(partA.RootIds), "profile A only sees A's builds");
var forB = jsonUtil.Deserialize<TraderAssort>(jsonUtil.Serialize(union))!;
BuildAssortService.Filter(partB, forB);
Check(forB.Items.Count == partB.Items.Count && forB.BarterScheme.Count == 1, "profile B only sees B's builds");
var forNone = jsonUtil.Deserialize<TraderAssort>(jsonUtil.Serialize(union))!;
BuildAssortService.Filter(null, forNone);
Check(forNone.Items.Count == 0 && forNone.BarterScheme.Count == 0 && forNone.LoyalLevelItems.Count == 0, "unknown profile sees nothing");
BuildAssortService.Filter(partA, null);
Check(true, "filter tolerates null result");

// ---------- 5. price ----------
double Eur(double r) => r / 150.0;
Check(BuildAssortService.FinalPrice(100000, cfg, Eur) == 67, "100000 RUB -> 666.7 EUR -90% -> 67");
Check(BuildAssortService.FinalPrice(0, cfg, Eur) == 12, "no price -> fallback cost 123 -90% -> 12");
Check(BuildAssortService.FinalPrice(100000, cfg2, Eur) == 1, "100% discount -> 1 (never free/0)");
Check(BuildAssortService.FinalPrice(100000, cfgRub, Eur) == 100000, "roubles, no discount -> no conversion");
Check(BuildAssortService.FinalPrice(10, cfg, Eur) == 1, "tiny price -> at least 1");

// ---------- 6. ids ----------
var d = BuildAssortService.DeriveId(new MongoId(), new MongoId());
Check(MongoId.IsValidMongoId(d.ToString()) && d.ToString().Length == 24, "derived id is a valid 24-hex MongoId");
Check(MongoId.IsValidMongoId(BuildAssortService.TraderIdString), "trader id is a valid MongoId");
var oldIdOk = true;
try { _ = new MongoId("hephaestus_alxk"); oldIdOk = true; } catch { oldIdOk = false; }
Check(!oldIdOk, "old 3.7 id 'hephaestus_alxk' is rejected by 4.1 (reason for the new id)");

// ---------- 7. patch wiring ----------
var target = typeof(TraderAssortHelper).GetMethod(nameof(TraderAssortHelper.GetAssort), BindingFlags.Public | BindingFlags.Instance);
Check(target is not null, "patch target TraderAssortHelper.GetAssort exists");
var targetParams = target!.GetParameters().Select(p => (p.Name, p.ParameterType)).ToList();
foreach (var m in typeof(GetAssortPatch).GetMethods(BindingFlags.Public | BindingFlags.Static).Where(m => m.DeclaringType == typeof(GetAssortPatch)))
{
    var attr = m.GetCustomAttributes().Any(a => a is PatchPrefixAttribute or PatchPostfixAttribute or PatchFinalizerAttribute);
    Check(attr, $"{m.Name} has a patch attribute");
    foreach (var p in m.GetParameters())
    {
        var ok = p.Name == "__result"
            ? p.ParameterType == target.ReturnType
            : targetParams.Contains((p.Name, p.ParameterType));
        Check(ok, $"{m.Name}({p.Name}) binds to the target");
    }
}

// prefix takes the lock for Hephaestus only, finalizer always releases it
var svc = (BuildAssortService)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(BuildAssortService));
var gateField = typeof(BuildAssortService).GetFields(BindingFlags.NonPublic | BindingFlags.Instance).First(f => f.Name == "Gate");
gateField.SetValue(svc, new object());
GetAssortPatch.Service = svc;
var other = new MongoId("54cb50c76803fa8b248b4571");
GetAssortPatch.Prefix(new MongoId(), other);
Check(!Monitor.IsEntered(svc.Gate), "other trader: no lock");
GetAssortPatch.Prefix(new MongoId(), BuildAssortService.TraderId); // rebuild throws inside (no server) and is caught
Check(Monitor.IsEntered(svc.Gate), "Hephaestus: lock held during GetAssort");
GetAssortPatch.Finalizer(BuildAssortService.TraderId);
Check(!Monitor.IsEntered(svc.Gate), "finalizer releases the lock");
GetAssortPatch.Finalizer(BuildAssortService.TraderId);
Check(true, "finalizer without lock is harmless");
GetAssortPatch.Service = null;

// ---------- 8. DI attributes ----------
Check(typeof(HephaestusLoader).GetCustomAttribute<Injectable>() is not null, "loader is [Injectable]");
var sa = typeof(BuildAssortService).GetCustomAttribute<Injectable>();
Check(sa is not null && sa.InjectionType == InjectionType.Singleton, "BuildAssortService is a singleton");

Console.WriteLine($"\n{pass} passed, {fail} failed");
return fail == 0 ? 0 : 1;

internal static class TestExt
{
    public static int GetItemWithChildrenCount(this List<Item> items, MongoId root) =>
        SPTarkov.Server.Core.Extensions.ItemExtensions.GetItemWithChildren(items, root).Count;
}
