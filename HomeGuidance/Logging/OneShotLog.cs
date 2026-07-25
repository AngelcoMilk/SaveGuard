using System.Collections.Generic;

namespace HomeGuidance.Logging;

public static class OneShotLog
{
    private static readonly HashSet<string> _fired = new();

    public static bool TryLog(string key, System.Action<string> log, string message)
    {
        if (_fired.Add(key))
        {
            log(message);
            return true;
        }
        return false;
    }

    public static void Reset() => _fired.Clear();
}
