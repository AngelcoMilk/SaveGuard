using HarmonyLib;
using HomeGuidance.Runtime;

namespace HomeGuidance.Patches;

[HarmonyPatch(typeof(YAPYAP.UICompass))]
public static class UICompassPatches
{
    [HarmonyPatch("Awake")]
    [HarmonyPostfix]
    private static void AwakePostfix(YAPYAP.UICompass __instance)
    {
        try
        {
            var controller = GuidanceRuntimeHost.TryGetController();
            if (controller != null)
                controller.AttachCompass(__instance);
        }
        catch (System.Exception ex)
        {
            GuidanceLog.Error($"UICompass.Awake Postfix failed: {ex.Message}");
        }
    }

    [HarmonyPatch(nameof(YAPYAP.UICompass.SetEnabled))]
    [HarmonyPostfix]
    private static void SetEnabledPostfix(YAPYAP.UICompass __instance, bool enabled)
    {
        try
        {
            var controller = GuidanceRuntimeHost.TryGetController();
            if (controller != null)
                controller.NotifyCompassEnabled(__instance, enabled);
        }
        catch (System.Exception ex)
        {
            GuidanceLog.Error($"UICompass.SetEnabled Postfix failed: {ex.Message}");
        }
    }

    [HarmonyPatch("OnDestroy")]
    [HarmonyPrefix]
    private static void OnDestroyPrefix(YAPYAP.UICompass __instance)
    {
        try
        {
            var controller = GuidanceRuntimeHost.TryGetController();
            if (controller != null)
                controller.DetachCompass(__instance);
        }
        catch (System.Exception ex)
        {
            GuidanceLog.Error($"UICompass.OnDestroy Prefix failed: {ex.Message}");
        }
    }
}
