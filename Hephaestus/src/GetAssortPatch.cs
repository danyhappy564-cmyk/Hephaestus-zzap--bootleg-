using System.Reflection;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Helpers.Traders;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace Hephaestus;

/// <summary>
/// Hooks TraderAssortHelper.GetAssort (the server function that hands a trader's goods to the game,
/// used both when the trader screen opens and when an offer is bought).
/// For Hephaestus only: rebuild the player's weapon-build offers right before, and keep only that
/// player's offers in the result.
///
/// The original mod rebuilt on "/launcher/profile/login" and on every "/client/game/profile/items/moving"
/// request, i.e. on every inventory action; rebuilding when the goods are actually requested
/// is cheaper and always up to date.
/// </summary>
public class GetAssortPatch : AbstractPatch
{
    internal static BuildAssortService? Service;

    public GetAssortPatch() : base("Hephaestus.GetAssortPatch")
    {
    }

    protected override MethodBase? GetTargetMethod()
    {
        return typeof(TraderAssortHelper).GetMethod(nameof(TraderAssortHelper.GetAssort),
            BindingFlags.Public | BindingFlags.Instance);
    }

    [PatchPrefix]
    public static void Prefix(MongoId sessionId, MongoId traderId)
    {
        var service = Service;
        if (service is null || traderId != BuildAssortService.TraderId)
        {
            return;
        }

        // Released in Finalizer (same thread; GetAssort is synchronous).
        Monitor.Enter(service.Gate);
        try
        {
            service.RebuildForSession(sessionId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Hephaestus] rebuild failed: {ex}");
        }
    }

    [PatchPostfix]
    public static void Postfix(MongoId sessionId, MongoId traderId, TraderAssort __result)
    {
        var service = Service;
        if (service is null || traderId != BuildAssortService.TraderId)
        {
            return;
        }

        try
        {
            service.FilterForSession(sessionId, __result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Hephaestus] filter failed: {ex}");
        }
    }

    [PatchFinalizer]
    public static void Finalizer(MongoId traderId)
    {
        var service = Service;
        if (service is not null && traderId == BuildAssortService.TraderId && Monitor.IsEntered(service.Gate))
        {
            Monitor.Exit(service.Gate);
        }
    }
}
