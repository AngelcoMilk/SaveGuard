using System.Collections.Generic;
using HarmonyLib;
using UnityEngine.InputSystem;
using YAPYAP;

namespace MoreSlots.Patches;

/// <summary>
/// Checks for extra-slot hotkey presses using simple Keyboard polling.
/// Does NOT modify the game's InputActionAsset — safe for player movement.
/// </summary>
[HarmonyPatch(typeof(UIInventory), "Update")]
public static class InventoryHotkeyPatch
{
    private static readonly Dictionary<int, Key> _slotKeys = new();
    private static int _lastMaxSlots = -1;

    private static void Postfix(UIInventory __instance)
    {
        if (!InventoryCapacityPatch.IsEnabled())
            return;

        PawnInventory inventory = Traverse.Create(__instance)
            .Field("_playerInventory")
            .GetValue<PawnInventory>();

        if (inventory == null || Keyboard.current == null)
            return;

        int maxSlots = InventoryCapacityPatch.GetMaxSlots();
        EnsureBindings(maxSlots);

        for (int i = 3; i < maxSlots; i++)
        {
            if (_slotKeys.TryGetValue(i, out Key key) &&
                Keyboard.current[key].wasPressedThisFrame)
            {
                if (i < inventory.Items.Count)
                    inventory.CmdSelectSlotWithMainHand(i);
                return;
            }
        }
    }

    private static void EnsureBindings(int maxSlots)
    {
        if (maxSlots == _lastMaxSlots) return;
        _lastMaxSlots = maxSlots;

        _slotKeys.Clear();

        for (int i = 3; i < maxSlots; i++)
        {
            string path = Plugin.GetKeyBinding(i);
            if (string.IsNullOrEmpty(path)) continue;

            Key parsed = ParseKey(path);
            if (parsed != Key.None)
                _slotKeys[i] = parsed;
        }
    }

    /// <summary>
    /// Parses "<Keyboard>/4", "<Keyboard>/f1", "4", "F1", etc. into a Key enum value.
    /// </summary>
    private static Key ParseKey(string path)
    {
        if (string.IsNullOrEmpty(path)) return Key.None;

        // Strip <Keyboard>/ prefix if present
        string keyName = path;
        const string prefix = "<Keyboard>/";
        if (keyName.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
            keyName = keyName.Substring(prefix.Length);

        if (string.IsNullOrEmpty(keyName)) return Key.None;

        // Try direct enum parse (handles "f1", "space", "leftShift", etc.)
        if (System.Enum.TryParse(keyName, true, out Key result))
            return result;

        // Handle digit chars: "4" → Digit4, "5" → Digit5
        if (keyName.Length == 1 && char.IsDigit(keyName[0]))
        {
            string digitKey = "Digit" + keyName;
            if (System.Enum.TryParse(digitKey, true, out Key digitResult))
                return digitResult;
        }

        return Key.None;
    }
}
