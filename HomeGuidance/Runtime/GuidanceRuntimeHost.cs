using UnityEngine;

namespace HomeGuidance.Runtime;

public sealed class GuidanceRuntimeHost : MonoBehaviour
{
    private const string HostObjectName = "HomeGuidanceRuntimeHost";

    public GuidanceController Controller { get; private set; }

    public static GuidanceRuntimeHost Create()
    {
        var existing = FindFirstObjectByType<GuidanceRuntimeHost>();
        if (existing != null)
        {
            GuidanceLog.Warning("Runtime host already exists; reusing.");
            return existing;
        }

        var go = new GameObject(HostObjectName);
        DontDestroyOnLoad(go);
        var host = go.AddComponent<GuidanceRuntimeHost>();
        host.Controller = new GuidanceController();
        return host;
    }

    private void Update()
    {
        if (Controller == null || !Plugin.ModConfig.Enabled.Value) return;

        float now = Time.time;
        float dt = Time.deltaTime;
        Controller.Tick(now, dt);
    }

    private void LateUpdate()
    {
        if (Controller == null || !Plugin.ModConfig.Enabled.Value) return;

        float now = Time.time;
        Controller.LateTick(now, UnityEngine.Time.deltaTime);
    }

    private void OnDestroy()
    {
        Controller = null;
    }

    public void Shutdown()
    {
        Controller?.Shutdown();
        Controller = null;
        if (this != null && gameObject != null)
            Destroy(gameObject);
    }

    public static GuidanceController TryGetController()
    {
        var host = FindFirstObjectByType<GuidanceRuntimeHost>();
        return host?.Controller;
    }
}
