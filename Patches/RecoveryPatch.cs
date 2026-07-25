using System.Reflection;
using HarmonyLib;
using UnityEngine;
using YAPYAP;

namespace SaveGuard.Patches;

[HarmonyPatch(typeof(LostItemsTracker), nameof(LostItemsTracker.SvRecoverDroppedItemsToLobby))]
internal static class RecoveryPatch
{
    private static readonly FieldInfo RecoveryChanceField = AccessTools.Field(typeof(LostItemsTracker), "droppedItemsRecoveryChance");
    private static readonly FieldInfo MaxRecoveryPercentageField = AccessTools.Field(typeof(LostItemsTracker), "maxItemsRecoveryPercentage");
    private static readonly FieldInfo PerPlayerCapField = AccessTools.Field(typeof(LostItemsTracker), "maxItemsToRecoveryPerNonExtractedPlayer");

    [HarmonyPrefix]
    private static void Prefix(LostItemsTracker __instance)
    {
        int percent = SaveGuardPolicy.NormalizeRecoveryPercent(Plugin.RecoveryPercent.Value);
        RecoveryChanceField.SetValue(__instance, SaveGuardPolicy.ToRecoveryChance(percent));

        if (percent == 0)
        {
            MaxRecoveryPercentageField.SetValue(__instance, 0f);
            PerPlayerCapField.SetValue(__instance, 0);
        }
        else
        {
            MaxRecoveryPercentageField.SetValue(__instance, 1f);
            PerPlayerCapField.SetValue(__instance, 1000000);
        }

        Plugin.Debug($"Applying failed-extraction recovery setting: {percent}%.");
    }
}
