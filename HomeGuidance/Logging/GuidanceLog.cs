using BepInEx.Logging;

namespace HomeGuidance;

public static class GuidanceLog
{
    public static ManualLogSource Source { get; set; }

    public static void Info(string message) => Source?.LogInfo(message);
    public static void Debug(string message) => Source?.LogDebug(message);
    public static void Warning(string message) => Source?.LogWarning(message);
    public static void Error(string message) => Source?.LogError(message);
}
