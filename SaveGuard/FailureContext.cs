namespace SaveGuard;

internal static class FailureContext
{
    internal static bool RestartScopeActive { get; private set; }
    internal static bool SoftFailureOccurred { get; private set; }
    internal static bool GameOverExecutionScope { get; private set; }

    internal static void BeginRestart(bool reachedQuota)
    {
        RestartScopeActive = !reachedQuota && Plugin.ProtectQuotaFailure?.Value == true;
        if (reachedQuota)
        {
            SoftFailureOccurred = false;
        }
    }

    internal static void MarkSoftFailure()
    {
        SoftFailureOccurred = true;
    }

    internal static void EndRestart()
    {
        RestartScopeActive = false;
    }

    internal static void BeginGameOverExecution()
    {
        GameOverExecutionScope = SoftFailureOccurred;
    }

    internal static void EndGameOverExecution()
    {
        GameOverExecutionScope = false;
        SoftFailureOccurred = false;
    }

    internal static void Reset()
    {
        RestartScopeActive = false;
        SoftFailureOccurred = false;
        GameOverExecutionScope = false;
    }
}
