using SaveGuard;

static void Equal<T>(T expected, T actual, string name)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{name}: expected {expected}, got {actual}");
    }
}

Equal(0, SaveGuardPolicy.ClampRecoveryPercent(-10), "Clamp low");
Equal(55, SaveGuardPolicy.ClampRecoveryPercent(55), "Clamp middle");
Equal(100, SaveGuardPolicy.ClampRecoveryPercent(120), "Clamp high");
Equal(0, SaveGuardPolicy.NormalizeRecoveryPercent(-10), "Normalize below zero");
Equal(0, SaveGuardPolicy.NormalizeRecoveryPercent(12), "Normalize to zero");
Equal(25, SaveGuardPolicy.NormalizeRecoveryPercent(13), "Normalize to twenty-five");
Equal(50, SaveGuardPolicy.NormalizeRecoveryPercent(55), "Normalize to fifty");
Equal(75, SaveGuardPolicy.NormalizeRecoveryPercent(87), "Normalize to seventy-five");
Equal(100, SaveGuardPolicy.NormalizeRecoveryPercent(88), "Normalize to one hundred");
Equal(0f, SaveGuardPolicy.ToRecoveryChance(0), "Zero chance");
Equal(0.5f, SaveGuardPolicy.ToRecoveryChance(55), "Normalized half chance");
Equal(1f, SaveGuardPolicy.ToRecoveryChance(100), "Full chance");

Equal(true, SaveGuardPolicy.ShouldUseSoftReset(true, true, false), "Soft reset enabled failure");
Equal(false, SaveGuardPolicy.ShouldUseSoftReset(true, true, true), "Successful quota stays native");
Equal(false, SaveGuardPolicy.ShouldUseSoftReset(true, false, false), "Startup reset stays native");
Equal(false, SaveGuardPolicy.ShouldUseSoftReset(false, true, false), "Protection disabled stays native");

Equal(true, SaveGuardPolicy.ShouldSuppressGameOverDelete(true, true, true), "Scoped Game Over call-site delete suppression");
Equal(false, SaveGuardPolicy.ShouldSuppressGameOverDelete(true, true, false), "Manual deletion unaffected");
Equal(false, SaveGuardPolicy.ShouldSuppressGameOverDelete(true, false, true), "Other game over unaffected");
Equal(false, SaveGuardPolicy.ShouldSuppressGameOverDelete(false, true, true), "Protection disabled delete native");

FailureContext.Reset();
FailureContext.BeginRestart(reachedQuota: false, protectQuotaFailure: true);
Equal(true, FailureContext.RestartScopeActive, "Failed quota restart scope begins");
Equal(true, FailureContext.QuotaFailurePending, "Failed quota restart immediately arms deletion protection");
FailureContext.EndRestart();
Equal(false, FailureContext.RestartScopeActive, "Restart scope ends after soft reset");
Equal(true, FailureContext.QuotaFailurePending, "Soft failure survives RestartGame completion");
FailureContext.BeginGameOverExecution();
Equal(true, FailureContext.GameOverExecutionScope, "Delayed Game Over inherits soft failure");
Equal(true, SaveGuardPolicy.ShouldSuppressGameOverDelete(
    enabled: true,
    quotaFailurePending: FailureContext.QuotaFailurePending,
    executionScope: FailureContext.GameOverExecutionScope), "Delayed Game Over deletion is suppressed");
FailureContext.EndGameOverExecution();
Equal(false, FailureContext.QuotaFailurePending, "Game Over completion clears soft failure");
Equal(false, FailureContext.GameOverExecutionScope, "Game Over completion clears execution scope");

FailureContext.BeginRestart(reachedQuota: false, protectQuotaFailure: true);
FailureContext.AbortRestart();
Equal(false, FailureContext.RestartScopeActive, "Restart exception clears restart scope");
Equal(true, FailureContext.QuotaFailurePending, "Restart exception preserves queued deletion protection");
Equal(false, FailureContext.GameOverExecutionScope, "Restart exception clears active Game Over scope");
FailureContext.BeginGameOverExecution();
Equal(true, FailureContext.GameOverExecutionScope, "Queued Game Over inherits post-exception protection");
Equal(true, SaveGuardPolicy.ShouldSuppressGameOverDelete(
    enabled: true,
    quotaFailurePending: FailureContext.QuotaFailurePending,
    executionScope: FailureContext.GameOverExecutionScope), "Post-exception Game Over deletion is suppressed");
FailureContext.EndGameOverExecution();
Equal(false, FailureContext.QuotaFailurePending, "Post-exception Game Over consumes protection");

FailureContext.BeginRestart(reachedQuota: false, protectQuotaFailure: true);
FailureContext.AbortRestart();
FailureContext.BeginRestart(reachedQuota: true, protectQuotaFailure: true);
Equal(false, FailureContext.RestartScopeActive, "Successful quota keeps native restart scope");
Equal(false, FailureContext.QuotaFailurePending, "Successful quota clears pending post-exception protection");

FailureContext.BeginRestart(reachedQuota: false, protectQuotaFailure: true);
FailureContext.AbortRestart();
FailureContext.BeginRestart(reachedQuota: false, protectQuotaFailure: false);
Equal(false, FailureContext.RestartScopeActive, "Disabled protection keeps native restart scope");
Equal(false, FailureContext.QuotaFailurePending, "Disabled protection clears pending protection");

FailureContext.BeginRestart(reachedQuota: false, protectQuotaFailure: true);
FailureContext.AbortRestart();
FailureContext.Reset();
Equal(false, FailureContext.QuotaFailurePending, "Server reset clears pending protection");

Console.WriteLine("SaveGuard policy and lifecycle tests passed.");
