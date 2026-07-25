using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;
using HarmonyLib;
using YAPYAP;
using UnityEngine;
using System;

namespace MoreSlots.Patches;

/// <summary>
/// Transpiler that replaces hardcoded slot count constants (3 and 2)
/// across all relevant PawnInventory methods.
/// </summary>
[HarmonyPatch]
public static class InventoryCapacityPatch
{
    // Methods whose ldc.i4.3 (slot count) and ldc.i4.2 (max index) need replacing
    private static readonly string[] TargetMethodNames =
    {
        "OnStartServer",
        "OnStartClient",
        "IsFull",
        "ServerTryAddItem",
        "ServerAddItemToSlot",
        "ServerRemoveFromSlot",
        "SelectSlotWithMainHand",
        "ServerSerializeToKvp",
        "ServerTryRestoreFromKvp",
        "UserCode_CmdCycleSlot__Boolean",
        "UserCode_CmdMoveItemInInventory__UInt32__Int32",
        "UserCode_CmdSwapSlotWithRightHand__Int32",
    };

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        Type type = typeof(PawnInventory);
        foreach (string methodName in TargetMethodNames)
        {
            MethodInfo method = AccessTools.Method(type, methodName);
            if (method != null)
                yield return method;
        }
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        foreach (CodeInstruction instruction in instructions)
        {
            if (!IsEnabled())
            {
                yield return instruction;
                continue;
            }

            if (IsLdcI4(instruction, 3))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = AccessTools.Method(
                    typeof(InventoryCapacityPatch),
                    nameof(GetMaxSlots)
                );
            }
            else if (IsLdcI4(instruction, 2))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = AccessTools.Method(
                    typeof(InventoryCapacityPatch),
                    nameof(GetMaxSlotIndex)
                );
            }

            yield return instruction;
        }
    }

    public static int GetMaxSlots()
    {
        if (!IsEnabled())
            return 3;
        return Mathf.Clamp(Plugin.MaxSlots.Value, 3, 10);
    }

    public static int GetMaxSlotIndex()
    {
        return GetMaxSlots() - 1;
    }

    public static bool IsEnabled()
    {
        return Plugin.EnableExtendedSlots?.Value == true;
    }

    private static bool IsLdcI4(CodeInstruction instruction, int value)
    {
        if (value == 2 && instruction.opcode == OpCodes.Ldc_I4_2) return true;
        if (value == 3 && instruction.opcode == OpCodes.Ldc_I4_3) return true;
        if (instruction.opcode == OpCodes.Ldc_I4 && instruction.operand is int i && i == value) return true;
        if (instruction.opcode == OpCodes.Ldc_I4_S && instruction.operand is sbyte s && s == value) return true;
        return false;
    }
}
