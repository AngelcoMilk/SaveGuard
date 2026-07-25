using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace HomeGuidance;

public static class BuildGuard
{
    public static string CurrentHash { get; private set; }
    public static bool IsSupported { get; private set; }
    public static string DisabledReason { get; private set; }

    public static bool Probe()
    {
        try
        {
            var asm = typeof(YAPYAP.GameManager).Assembly;
            var location = asm.Location;
            if (string.IsNullOrEmpty(location) || !File.Exists(location))
            {
                DisabledReason = "Assembly-CSharp location not found";
                return false;
            }

            using var sha = SHA256.Create();
            using var stream = File.OpenRead(location);
            var hash = sha.ComputeHash(stream);
            CurrentHash = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

            IsSupported = SupportedGameBuilds.IsAllowed(CurrentHash);
            if (!IsSupported)
            {
                DisabledReason = $"Build hash not in supported list: {CurrentHash}";
                return false;
            }

            // Probe required members
            if (!ProbeRequiredMembers())
            {
                return false;
            }

            GuidanceLog.Info($"Build guard: supported, SHA256={CurrentHash}");
            return true;
        }
        catch (Exception ex)
        {
            DisabledReason = $"Build guard exception: {ex.Message}";
            GuidanceLog.Error(DisabledReason);
            return false;
        }
    }

    private static bool ProbeRequiredMembers()
    {
        int required = 0;
        int found = 0;

        void Probe(string label, Func<bool> check)
        {
            required++;
            if (check()) found++;
            else GuidanceLog.Warning($"Required member missing: {label}");
        }

        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        Probe("UICompass.Awake", () => typeof(YAPYAP.UICompass).GetMethod("Awake", Flags) != null);
        Probe("UICompass.SetEnabled", () => typeof(YAPYAP.UICompass).GetMethod("SetEnabled", Flags) != null);
        Probe("UICompass.OnDestroy", () => typeof(YAPYAP.UICompass).GetMethod("OnDestroy", Flags) != null);
        Probe("UICompass._visualRefs", () => typeof(YAPYAP.UICompass).GetField("_visualRefs", Flags) != null);
        Probe("UICompass.currentTarget", () => typeof(YAPYAP.UICompass).GetField("currentTarget", Flags) != null);
        Probe("UICompass.compassActiveFrame", () => typeof(YAPYAP.UICompass).GetField("compassActiveFrame", Flags) != null);
        Probe("UICompass.compassInactiveFrame", () => typeof(YAPYAP.UICompass).GetField("compassInactiveFrame", Flags) != null);
        Probe("UICompassReferences.RenderTargetRoot", () => typeof(YAPYAP.UICompassReferences).GetField("RenderTargetRoot", Flags) != null);
        Probe("UICompassReferences.CardinalDirectionsPivot", () => typeof(YAPYAP.UICompassReferences).GetField("CardinalDirectionsPivot", Flags) != null);
        Probe("UICompassReferences.TargetObjectPivot", () => typeof(YAPYAP.UICompassReferences).GetField("TargetObjectPivot", Flags) != null);
        Probe("UICompassReferences.ElevationIndicatorPivot", () => typeof(YAPYAP.UICompassReferences).GetField("ElevationIndicatorPivot", Flags) != null);
        Probe("Pawn.OnTeleport", () => typeof(YAPYAP.Pawn).GetMethod("OnTeleport", Flags) != null);
        Probe("TeleportDeadEndCircle.NetworkpairedCircle", () => typeof(YAPYAP.TeleportDeadEndCircle).GetProperty("NetworkpairedCircle", Flags) != null);
        Probe("TeleportDeadEndCircle.NetworkisInExtractionMode", () => typeof(YAPYAP.TeleportDeadEndCircle).GetProperty("NetworkisInExtractionMode", Flags) != null);
        Probe("TeleportDeadEndCircle.NetworkcountdownSecondsLeft", () => typeof(YAPYAP.TeleportDeadEndCircle).GetProperty("NetworkcountdownSecondsLeft", Flags) != null);
        Probe("TeleportDeadEndCircle.Networkstate", () => typeof(YAPYAP.TeleportDeadEndCircle).GetProperty("Networkstate", Flags) != null);

        GuidanceLog.Info($"Reflection probe: {found}/{required} required members");
        if (found < required)
        {
            DisabledReason = $"Missing {required - found} required members";
            return false;
        }
        return true;
    }
}
