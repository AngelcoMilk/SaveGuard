using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace SaveGuard;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.saveguard.yapyap";
    public const string PluginName = "SaveGuard";
    public const string PluginVersion = "0.1.0";
    internal const string SupportedAssemblyHash = "7b6ef048e716ce4cf87bf5c6f190b3c11d39c50aa18a81467770f13ceed3c542";

    internal static Plugin Instance;
    internal static ManualLogSource Log;

    internal static ConfigEntry<bool> ProtectQuotaFailure;
    internal static ConfigEntry<int> RecoveryPercent;
    internal static ConfigEntry<bool> CreateEmergencyBackup;
    internal static ConfigEntry<int> MaxEmergencyBackups;
    internal static ConfigEntry<bool> EnforceBuildGuard;
    internal static ConfigEntry<bool> DebugLog;

    private Harmony _harmony;

    private void Awake()
    {
        Instance = this;
        Log = Logger;

        ProtectQuotaFailure = Config.Bind("Quota Failure", "ProtectSave", true,
            "Keep the current save and restart the same quota session from night one after failure.");
        RecoveryPercent = Config.Bind("Recovery", "RecoveryPercent", 100,
            new ConfigDescription("Chance for each eligible dropped item to return to the lobby.",
                new AcceptableValueList<int>(0, 25, 50, 75, 100)));
        CreateEmergencyBackup = Config.Bind("Safety", "CreateEmergencyBackup", true,
            "Copy the current save before applying the quota-failure soft reset.");
        MaxEmergencyBackups = Config.Bind("Safety", "MaxEmergencyBackups", 5,
            new ConfigDescription("Maximum SaveGuard backup files retained per profile.",
                new AcceptableValueRange<int>(1, 20)));
        EnforceBuildGuard = Config.Bind("Compatibility", "EnforceBuildGuard", true,
            "Only install gameplay patches on the locally verified YAPYAP Assembly-CSharp build.");
        DebugLog = Config.Bind("General", "DebugLog", false, "Enable verbose SaveGuard logging.");

        if (!CompatibilityGuard.Validate(SupportedAssemblyHash, EnforceBuildGuard.Value, out string reason))
        {
            Logger.LogError("Compatibility validation failed; SaveGuard gameplay patches were not installed. " + reason);
            return;
        }

        _harmony = new Harmony(PluginGuid);
        _harmony.PatchAll(Assembly.GetExecutingAssembly());
        Logger.LogInfo("SaveGuard loaded: quota-failure protection and configurable item recovery are active.");
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
        FailureContext.Reset();
        Instance = null;
        Log = null;
    }

    internal static void Debug(string message)
    {
        if (DebugLog?.Value == true)
        {
            Log?.LogInfo(message);
        }
    }
}
