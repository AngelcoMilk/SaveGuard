namespace SaveGuard;

internal static class FailureContext
{
    internal static bool RestartScopeActive { get; private set; }
    internal static bool QuotaFailurePending { get; private set; }
    internal static bool GameOverExecutionScope { get; private set; }

    internal static void BeginRestart(bool reachedQuota, bool protectQuotaFailure)
    {
        RestartScopeActive = !reachedQuota && protectQuotaFailure;
        QuotaFailurePending = RestartScopeActive;
        GameOverExecutionScope = false;
    }

    internal static void EndRestart()
    {
        RestartScopeActive = false;
    }

    internal static void AbortRestart()
    {
        RestartScopeActive = false;
        GameOverExecutionScope = false;
    }

    internal static void BeginGameOverExecution()
    {
        GameOverExecutionScope = QuotaFailurePending;
    }

    internal static void EndGameOverExecution()
    {
        GameOverExecutionScope = false;
        QuotaFailurePending = false;
    }

    internal static void Reset()
    {
        RestartScopeActive = false;
        QuotaFailurePending = false;
        GameOverExecutionScope = false;
    }
}
