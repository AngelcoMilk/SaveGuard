using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;
using YAPYAP;

namespace MoreSlots.Patches;

/// <summary>
/// Serializes and restores extended inventory slots (4+) to/from the save file.
/// Uses the same PLAYER.{id}.INV key prefix convention as Artisan for compatibility.
/// </summary>
[HarmonyPatch]
public static class InventoryPersistencePatch
{
    private static readonly HashSet<PawnInventory> RestoringInventories = new();

    [HarmonyPatch(typeof(PawnInventory), "ServerSerializeToKvp")]
    [HarmonyPostfix]
    private static void SerializePostfix(PawnInventory __instance, SaveManager save, string playerId)
    {
        if (!CanHandle(__instance, save, playerId)) return;
        if (RestoringInventories.Contains(__instance)) return;

        try
        {
            SerializeExtended(__instance, save, playerId);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[MoreSlots] Serialize failed: {ex.Message}");
        }
    }

    [HarmonyPatch(typeof(PawnInventory), "ServerTryRestoreFromKvp")]
    [HarmonyPrefix]
    private static void RestorePrefix(PawnInventory __instance, SaveManager save, string playerId)
    {
        if (!CanHandle(__instance, save, playerId)) return;
        RestoringInventories.Add(__instance);

        try
        {
            HandleSlotCountMismatch(__instance, save, playerId);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[MoreSlots] Restore prefix failed: {ex.Message}");
        }
    }

    [HarmonyPatch(typeof(PawnInventory), "ServerTryRestoreFromKvp")]
    [HarmonyPostfix]
    private static void RestorePostfix(PawnInventory __instance, SaveManager save, string playerId)
    {
        if (!CanHandle(__instance, save, playerId)) return;

        try
        {
            RestoreExtended(__instance, save, playerId);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[MoreSlots] Restore postfix failed: {ex.Message}");
        }
        finally
        {
            RestoringInventories.Remove(__instance);
        }
    }

    private static bool CanHandle(PawnInventory inventory, SaveManager save, string playerId)
    {
        return inventory != null &&
               save != null &&
               !string.IsNullOrEmpty(playerId) &&
               InventoryCapacityPatch.IsEnabled();
    }

    // ── Serialize ──

    private static void SerializeExtended(PawnInventory inventory, SaveManager save, string playerId)
    {
        string prefix = GetKeyPrefix(playerId);
        int maxSlots = InventoryCapacityPatch.GetMaxSlots();

        save.SetInt(prefix + ".SLOT_COUNT", maxSlots);
        save.SetInt(prefix + ".MAIN", Mathf.Clamp(inventory.CurrentMainHandSlot, 0, maxSlots - 1));

        MethodInfo serializeProp = AccessTools.Method(typeof(PawnInventory), "SerializeProp");
        if (serializeProp == null) return;

        for (int i = 3; i < maxSlots; i++)
        {
            NetworkPuppetProp prop = GetSlotProp(inventory, i);
            serializeProp.Invoke(inventory, new object[] { save, $"{prefix}.S{i}", prop });
        }
    }

    // ── Restore ──

    private static void HandleSlotCountMismatch(PawnInventory inventory, SaveManager save, string playerId)
    {
        string prefix = GetKeyPrefix(playerId);
        if (!save.TryGetInt(prefix + ".SLOT_COUNT", out int savedCount))
            return;

        int currentMax = InventoryCapacityPatch.GetMaxSlots();
        if (savedCount <= currentMax)
            return;

        // Slot count reduced — drop overflow items
        MethodInfo tryRestoreProp = AccessTools.Method(typeof(PawnInventory), "TryRestoreProp");
        MethodInfo serializeProp = AccessTools.Method(typeof(PawnInventory), "SerializeProp");
        if (tryRestoreProp == null || serializeProp == null) return;

        for (int i = currentMax; i < savedCount; i++)
        {
            string slotKey = $"{prefix}.S{i}";
            NetworkPuppetProp prop = TryRestoreProp(inventory, save, playerId, slotKey, tryRestoreProp);
            if (prop == null) continue;

            DropProp(prop);
            serializeProp.Invoke(inventory, new object[] { save, slotKey, null });

            Plugin.Log?.LogInfo($"[MoreSlots] Dropped overflow item from slot {i + 1} (max now {currentMax}).");
        }
    }

    private static void RestoreExtended(PawnInventory inventory, SaveManager save, string playerId)
    {
        string prefix = GetKeyPrefix(playerId);
        int maxSlots = InventoryCapacityPatch.GetMaxSlots();

        MethodInfo tryRestoreProp = AccessTools.Method(typeof(PawnInventory), "TryRestoreProp");
        MethodInfo serverAddItemToSlot = AccessTools.Method(typeof(PawnInventory), "ServerAddItemToSlot");
        if (tryRestoreProp == null || serverAddItemToSlot == null) return;

        for (int i = 3; i < maxSlots; i++)
        {
            // Skip already-restored vanilla slots
            if (i < inventory.Items.Count && !inventory.Items[i].IsEmpty)
                continue;

            NetworkPuppetProp prop = TryRestoreProp(inventory, save, playerId, $"{prefix}.S{i}", tryRestoreProp);
            if (prop == null) continue;

            bool added = (bool)serverAddItemToSlot.Invoke(inventory, new object[]
            {
                i,
                new InventoryItem(prop)
            });

            if (added)
                SetPropInInventory(prop, inventory);
            else
                DropProp(prop);
        }

        // Restore selected main hand slot
        if (save.TryGetInt(prefix + ".MAIN", out int savedMain))
        {
            savedMain = Mathf.Clamp(savedMain, 0, maxSlots - 1);
            if (GetSlotProp(inventory, savedMain) != null)
                inventory.SelectSlotWithMainHand(savedMain);
        }

        inventory.ServerPersistInventory();
    }

    // ── Helpers ──

    private static NetworkPuppetProp TryRestoreProp(PawnInventory inventory, SaveManager save,
        string playerId, string key, MethodInfo method)
    {
        object[] args = { save, key, playerId, null };
        bool restored = (bool)method.Invoke(inventory, args);
        return restored ? args[3] as NetworkPuppetProp : null;
    }

    private static NetworkPuppetProp GetSlotProp(PawnInventory inventory, int index)
    {
        if (index < 0 || index >= inventory.Items.Count) return null;
        InventoryItem item = inventory.Items[index];
        return item.IsEmpty ? null : item.PropInstance;
    }

    private static void DropProp(NetworkPuppetProp prop)
    {
        if (prop == null) return;
        prop.IsShopItem = false;
        prop.ServerHandleDrop(true, default(Vector3));
        prop.RefreshLocalInteractable();
    }

    private static void SetPropInInventory(NetworkPuppetProp prop, PawnInventory inventory)
    {
        if (prop == null || inventory == null) return;

        var interactionsField = AccessTools.Field(typeof(PawnInventory), "propInteractions");
        PawnPropInteractions interactions = interactionsField?.GetValue(inventory) as PawnPropInteractions;

        if (interactions != null)
        {
            prop.ServerSetInInventory(interactions);
            return;
        }

        prop.CurrentState = new NetworkPuppetProp.PropStateData(
            PropState.InInventory, null, true);
    }

    private static string GetKeyPrefix(string playerId)
    {
        return $"PLAYER.{playerId}.INV";
    }
}
