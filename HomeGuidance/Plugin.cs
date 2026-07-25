using System.Reflection;
using BepInEx;
using HarmonyLib;
using HomeGuidance.Runtime;

namespace HomeGuidance;

[BepInPlugin(PluginInfo.PluginGuid, PluginInfo.PluginName, PluginInfo.PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public static Plugin Instance { get; private set; }
    public static HomeGuidanceConfig ModConfig { get; private set; }

    private Harmony _harmony;
    private GuidanceRuntimeHost _host;

    private void Awake()
    {
        Instance = this;
        GuidanceLog.Source = Logger;

        Logger.LogInfo($"HomeGuidance v{PluginInfo.PluginVersion} initializing...");

        ModConfig = new HomeGuidanceConfig(base.Config);
        ModConfig.Normalize();

        if (!BuildGuard.Probe())
        {
            Logger.LogWarning($"HomeGuidance disabled: {BuildGuard.DisabledReason}");
            return;
        }

        try
        {
            _host = GuidanceRuntimeHost.Create();
            if (_host == null)
            {
                Logger.LogError("HomeGuidance: failed to create runtime host");
                return;
            }

            _host.Controller.Initialize();

            _harmony = new Harmony(PluginInfo.PluginGuid);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());

            Logger.LogInfo("HomeGuidance loaded in client-local mode; no custom Mirror messages.");
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"HomeGuidance initialization failed: {ex}");
            try { _harmony?.UnpatchSelf(); } catch { }
            try { _host?.Shutdown(); } catch { }
            _host = null;
        }
    }

    private void OnDestroy()
    {
        Logging.OneShotLog.Reset();
        _harmony?.UnpatchSelf();
        _harmony = null;
        _host?.Shutdown();
        _host = null;
        Instance = null;
        GuidanceLog.Source = null;
    }
}
