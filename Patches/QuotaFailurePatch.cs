using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using YAPYAP;

namespace SaveGuard.Patches;

[HarmonyPatch]
internal static class QuotaFailurePatch
{
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.OnStartServer))]
    [HarmonyPrefix]
    private static void OnStartServerPrefix()
    {
        FailureContext.Reset();
    }

    [HarmonyPatch(typeof(GameManager), nameof(GameManager.RestartGame))]
    [HarmonyPrefix]
    private static void RestartGamePrefix(GameManager __instance, bool reachedQuota)
    {
        FailureContext.BeginRestart(reachedQuota);
        if (FailureContext.RestartScopeActive)
        {
            SaveBackupService.TryCreateQuotaFailureBackup(__instance);
        }
    }

    [HarmonyPatch(typeof(GameManager), nameof(GameManager.RestartGame))]
    [HarmonyPostfix]
    private static void RestartGamePostfix()
    {
        FailureContext.EndRestart();
    }

    [HarmonyPatch(typeof(GameManager), nameof(GameManager.RestartGame))]
    [HarmonyFinalizer]
    private static Exception RestartGameFinalizer(Exception __exception)
    {
        FailureContext.EndRestart();
        return __exception;
    }

    [HarmonyPatch(typeof(GameManager), "SvResetGameState")]
    [HarmonyPrefix]
    private static bool ResetGameStatePrefix(GameManager __instance, bool reachedQuota)
    {
        if (!SaveGuardPolicy.ShouldUseSoftReset(
                Plugin.ProtectQuotaFailure.Value,
                FailureContext.RestartScopeActive,
                reachedQuota))
        {
            return true;
        }

        if (DungeonTasks.Instance != null && DungeonManager.Instance != null)
        {
            DungeonTasks.Instance.CreateTasks(DungeonManager.Instance.Generator);
        }

        __instance.NetworkcurrentGameState = GameManager.GameState.Lobby;
        __instance.NetworkcurrentRound = 0;
        __instance.NetworktotalScore = 0;
        FailureContext.MarkSoftFailure();
        Plugin.Log?.LogInfo("Quota failure converted to a soft reset: save, gold, inventory, quota tier, hub and grimoire were preserved.");
        return false;
    }

    [HarmonyPatch(typeof(GameManager), "SvExecuteGameOver")]
    [HarmonyPrefix]
    private static void ExecuteGameOverPrefix()
    {
        FailureContext.BeginGameOverExecution();
    }

    [HarmonyPatch(typeof(GameManager), "SvExecuteGameOver")]
    [HarmonyPostfix]
    private static void ExecuteGameOverPostfix()
    {
        FailureContext.EndGameOverExecution();
    }

    [HarmonyPatch(typeof(GameManager), "SvExecuteGameOver")]
    [HarmonyFinalizer]
    private static Exception ExecuteGameOverFinalizer(Exception __exception)
    {
        FailureContext.EndGameOverExecution();
        return __exception;
    }

    [HarmonyPatch(typeof(GameManager), "SvExecuteGameOver")]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ExecuteGameOverTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo deleteSlotMethod = AccessTools.Method(typeof(SaveManager), nameof(SaveManager.DeleteSlot));
        MethodInfo scopedDeleteSlotMethod = AccessTools.Method(typeof(QuotaFailurePatch), nameof(ScopedDeleteSlot));
        if (deleteSlotMethod == null || scopedDeleteSlotMethod == null)
        {
            throw new MissingMethodException("Unable to resolve the quota-failure delete call replacement.");
        }

        int replacements = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.Calls(deleteSlotMethod))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = scopedDeleteSlotMethod;
                replacements++;
            }

            yield return instruction;
        }

        if (replacements != 1)
        {
            throw new InvalidOperationException($"Expected one SaveManager.DeleteSlot call in SvExecuteGameOver, found {replacements}.");
        }
    }

    private static void ScopedDeleteSlot(SaveManager saveManager, int slot)
    {
        bool suppress = SaveGuardPolicy.ShouldSuppressGameOverDelete(
            Plugin.ProtectQuotaFailure.Value,
            FailureContext.SoftFailureOccurred,
            FailureContext.GameOverExecutionScope);

        if (suppress && slot == saveManager.CurrentSlot)
        {
            Plugin.Log?.LogInfo($"Skipped quota-failure deletion of save slot {slot} while preserving the complete Game Over flow.");
            return;
        }

        saveManager.DeleteSlot(slot);
    }
}
