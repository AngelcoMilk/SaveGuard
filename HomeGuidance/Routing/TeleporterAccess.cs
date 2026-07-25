using System;
using System.Reflection;
using HarmonyLib;

namespace HomeGuidance.Routing;

public static class TeleporterAccess
{
    private static MethodInfo _networkStateGetter;

    static TeleporterAccess()
    {
        _networkStateGetter = AccessTools.PropertyGetter(typeof(YAPYAP.TeleportDeadEndCircle), "Networkstate");
    }

    public static bool TryReadPaired(YAPYAP.TeleportDeadEndCircle source, out YAPYAP.TeleportDeadEndCircle target)
    {
        target = source.NetworkpairedCircle;
        return target != null;
    }

    public static bool TryReadExtractionMode(YAPYAP.TeleportDeadEndCircle source, out bool value)
    {
        value = source.NetworkisInExtractionMode;
        return true;
    }

    public static bool TryReadCountdown(YAPYAP.TeleportDeadEndCircle source, out int seconds)
    {
        seconds = source.NetworkcountdownSecondsLeft;
        return true;
    }

    public static bool TryReadStateCode(YAPYAP.TeleportDeadEndCircle source, out int stateCode)
    {
        stateCode = 0;
        try
        {
            if (_networkStateGetter == null) return false;
            var boxed = _networkStateGetter.Invoke(source, null);
            if (boxed == null) return false;
            stateCode = Convert.ToInt32(boxed);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryReadPrefabTiming(YAPYAP.TeleportDeadEndCircle source, out float countdown, out float wait)
    {
        countdown = 0f;
        wait = 0f;
        try
        {
            var countdownField = typeof(YAPYAP.TeleportDeadEndCircle).GetField("countdownDuration",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var waitField = typeof(YAPYAP.TeleportDeadEndCircle).GetField("teleportWaitTime",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (countdownField != null) countdown = (float)countdownField.GetValue(source);
            if (waitField != null) wait = (float)waitField.GetValue(source);
            return countdownField != null && waitField != null;
        }
        catch
        {
            return false;
        }
    }
}
