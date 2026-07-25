using HarmonyLib;
using HomeGuidance.Runtime;

namespace HomeGuidance.Patches;

[HarmonyPatch(typeof(YAPYAP.Pawn))]
public static class PawnTeleportPatch
{
    [HarmonyPatch("OnTeleport")]
    [HarmonyPostfix]
    private static void OnTeleportPostfix(YAPYAP.Pawn __instance)
    {
        try
        {
            if (!__instance.isLocalPlayer) return;

            var controller = GuidanceRuntimeHost.TryGetController();
            if (controller != null)
                controller.NotifyLocalPawnTeleported(__instance);
        }
        catch (System.Exception ex)
        {
            GuidanceLog.Error($"Pawn.OnTeleport Postfix failed: {ex.Message}");
        }
    }
}
