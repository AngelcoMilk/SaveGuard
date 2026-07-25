using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace MoreSlots;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.angelcomilk.moreslots";
    public const string PluginName = "MoreSlots";
    public const string PluginVersion = "0.1.0";

    internal static Plugin Instance;
    internal static ManualLogSource Log;

    // ── Core ──
    internal static ConfigEntry<bool> EnableExtendedSlots;
    internal static ConfigEntry<int> MaxSlots;

    // ── Key bindings ──
    internal static ConfigEntry<string> KeySlot4;
    internal static ConfigEntry<string> KeySlot5;
    internal static ConfigEntry<string> KeySlot6;
    internal static ConfigEntry<string> KeySlot7;
    internal static ConfigEntry<string> KeySlot8;
    internal static ConfigEntry<string> KeySlot9;
    internal static ConfigEntry<string> KeySlot10;

    // ── UI ──
    internal static ConfigEntry<bool> ExpandInventoryUI;
    internal static ConfigEntry<bool> HideInventoryFrame;

    private Harmony _harmony;

    private void Awake()
    {
        Instance = this;
        Log = Logger;

        BindConfig();

        _harmony = new Harmony(PluginGuid);
        _harmony.PatchAll(Assembly.GetExecutingAssembly());

        Logger.LogInfo($"MoreSlots v{PluginVersion} loaded: max {MaxSlots.Value} slots.");
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
        Instance = null;
        Log = null;
    }

    private void BindConfig()
    {
        EnableExtendedSlots = Config.Bind("General", "EnableExtendedSlots", true,
            "Enable or disable extended inventory slots.");

        MaxSlots = Config.Bind("General", "MaxSlots", 6,
            new ConfigDescription("Maximum inventory slots. Vanilla is 3. Range: 3–10.",
                new AcceptableValueRange<int>(3, 10)));

        KeySlot4 = Config.Bind("KeyBindings", "Slot4", "<Keyboard>/4",
            "Input binding for inventory slot 4.");
        KeySlot5 = Config.Bind("KeyBindings", "Slot5", "<Keyboard>/5",
            "Input binding for inventory slot 5.");
        KeySlot6 = Config.Bind("KeyBindings", "Slot6", "<Keyboard>/6",
            "Input binding for inventory slot 6.");
        KeySlot7 = Config.Bind("KeyBindings", "Slot7", "<Keyboard>/7",
            "Input binding for inventory slot 7.");
        KeySlot8 = Config.Bind("KeyBindings", "Slot8", "<Keyboard>/8",
            "Input binding for inventory slot 8.");
        KeySlot9 = Config.Bind("KeyBindings", "Slot9", "",
            "Input binding for inventory slot 9. Leave empty for no binding.");
        KeySlot10 = Config.Bind("KeyBindings", "Slot10", "",
            "Input binding for inventory slot 10. Leave empty for no binding.");

        ExpandInventoryUI = Config.Bind("UI", "ExpandInventoryUI", true,
            "Clone the vanilla slot UI so extra slots are visible and interactive.");

        HideInventoryFrame = Config.Bind("UI", "HideInventoryFrame", true,
            "Hide the vanilla inventory background frame (designed for 3 slots).");
    }

    internal static string GetKeyBinding(int slotIndex)
    {
        return slotIndex switch
        {
            3 => KeySlot4.Value,
            4 => KeySlot5.Value,
            5 => KeySlot6.Value,
            6 => KeySlot7.Value,
            7 => KeySlot8.Value,
            8 => KeySlot9.Value,
            9 => KeySlot10.Value,
            _ => ""
        };
    }

    internal static void Debug(string message)
    {
        // verbose logging — keep for dev but silent in release
    }
}
