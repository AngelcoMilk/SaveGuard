namespace SaveGuard;

internal static class SaveGuardPolicy
{
    internal static int ClampRecoveryPercent(int value)
    {
        if (value < 0) return 0;
        if (value > 100) return 100;
        return value;
    }

    internal static int NormalizeRecoveryPercent(int value)
    {
        int clamped = ClampRecoveryPercent(value);
        if (clamped < 13) return 0;
        if (clamped < 38) return 25;
        if (clamped < 63) return 50;
        if (clamped < 88) return 75;
        return 100;
    }

    internal static float ToRecoveryChance(int percent)
    {
        return NormalizeRecoveryPercent(percent) / 100f;
    }

    internal static bool ShouldUseSoftReset(bool enabled, bool scopeActive, bool reachedQuota)
    {
        return enabled && scopeActive && !reachedQuota;
    }

    internal static bool ShouldSuppressGameOverDelete(bool enabled, bool quotaFailurePending, bool executionScope)
    {
        return enabled && quotaFailurePending && executionScope;
    }
}
