using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YAPYAP;

namespace MoreSlots.Patches;

/// <summary>
/// Clones UIInventorySlot GameObjects and InventoryItem frames,
/// positioning them horizontally to the right of the vanilla slots.
/// </summary>
[HarmonyPatch(typeof(UIInventory), "InitializeSlots")]
public static class InventoryUIPatch
{
    private const float SlotSpacingX = 52f;   // horizontal gap between slot centers
    private const float SlotSizeX = 42f;      // approximate slot width

    public static void Postfix(UIInventory __instance)
    {
        if (__instance == null || !InventoryCapacityPatch.IsEnabled())
            return;

        if (!Plugin.ExpandInventoryUI.Value)
            return;

        UIInventorySlot[] slots = Traverse.Create(__instance)
            .Field("inventorySlots")
            .GetValue<UIInventorySlot[]>();

        if (slots == null || slots.Length == 0 || slots[0] == null)
            return;

        int targetCount = InventoryCapacityPatch.GetMaxSlots();
        if (slots.Length >= targetCount)
            return;

        UIInventorySlot template = slots[0];
        Transform parent = template.transform.parent;

        // Get the anchored position of slot 0 as reference
        RectTransform slot0Rect = template.GetComponent<RectTransform>();
        Vector2 basePos = slot0Rect != null ? slot0Rect.anchoredPosition : Vector2.zero;

        // ── Clone slots ──
        UIInventorySlot[] expanded = new UIInventorySlot[targetCount];
        for (int i = 0; i < slots.Length; i++)
            expanded[i] = slots[i];

        for (int i = slots.Length; i < targetCount; i++)
        {
            GameObject clone = Object.Instantiate(template.gameObject, parent);
            clone.name = $"MoreSlots_InventorySlot_{i}";
            clone.SetActive(true);

            UIInventorySlot slot = clone.GetComponent<UIInventorySlot>();
            if (slot == null)
            {
                Object.Destroy(clone);
                continue;
            }

            // Update hotkey number text
            Transform keyTransform = clone.transform.Find("Key");
            if (keyTransform != null)
            {
                TMP_Text keyText = keyTransform.GetComponent<TMP_Text>();
                if (keyText != null)
                    keyText.text = (i + 1).ToString();
            }

            // Position horizontally to the right
            RectTransform rect = clone.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition = new Vector2(
                    basePos.x + (i - 0) * SlotSpacingX,
                    basePos.y
                );
            }

            // Disable layout so manual positioning works
            LayoutElement layout = clone.GetComponent<LayoutElement>();
            if (layout != null)
                layout.ignoreLayout = true;

            expanded[i] = slot;
        }

        Traverse.Create(__instance)
            .Field("inventorySlots")
            .SetValue(expanded);

        // ── Clone and reposition InventoryItem frames ──
        CloneAndPositionFrames(__instance, parent, targetCount, basePos);

        // ── Hide vanilla frame if configured ──
        if (Plugin.HideInventoryFrame.Value)
        {
            Transform frame = __instance.transform.Find("InventoryPanel/BottomRight/InventoryFrame");
            if (frame != null)
                frame.gameObject.SetActive(false);
        }
    }

    private static void CloneAndPositionFrames(UIInventory inventory, Transform slotParent,
        int slotCount, Vector2 baseSlotPos)
    {
        // Find frame container
        Transform frameContainer = inventory.transform.Find("InventoryPanel/BottomRight/InventoryContainer");
        if (frameContainer == null)
            return;

        // Collect existing frames
        List<RectTransform> existingFrames = new();
        for (int i = 0; i < frameContainer.childCount; i++)
        {
            Transform child = frameContainer.GetChild(i);
            if (child != null && child.name == "InventoryItem")
            {
                RectTransform rect = child.GetComponent<RectTransform>();
                if (rect != null)
                    existingFrames.Add(rect);
            }
        }

        if (existingFrames.Count == 0) return;

        // Clone to target count and position
        RectTransform template = existingFrames[0];
        Vector2 frameBasePos = template.anchoredPosition;

        while (existingFrames.Count < slotCount)
        {
            int idx = existingFrames.Count;
            RectTransform clone = Object.Instantiate(template, frameContainer);
            clone.name = "InventoryItem";
            clone.anchoredPosition = new Vector2(
                frameBasePos.x + (idx - 0) * SlotSpacingX,
                frameBasePos.y
            );
            clone.gameObject.SetActive(true);
            existingFrames.Add(clone);
        }

        // Hide excess frames
        for (int i = slotCount; i < existingFrames.Count; i++)
        {
            if (existingFrames[i] != null)
                existingFrames[i].gameObject.SetActive(false);
        }
    }
}
