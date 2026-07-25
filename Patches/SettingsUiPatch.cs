using System;
using HarmonyLib;
using YAPYAP;

namespace SaveGuard.Patches;

[HarmonyPatch(typeof(UISettings), nameof(UISettings.Initialise))]
internal static class SettingsUiPatch
{
    [HarmonyPrefix]
    private static void Prefix(UISettings __instance)
    {
        try
        {
            SettingsUiInjector.TryInject(__instance);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("Unable to inject the SaveGuard Settings tab: " + ex);
        }
    }
}
