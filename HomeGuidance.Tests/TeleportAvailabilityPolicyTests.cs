namespace HomeGuidance.Tests;

public static class TeleportAvailabilityPolicyTests
{
    public static void RunAll()
    {
        IdleAlwaysAvailable();
        ActivatingBeforeSweepAvailable();
        ActivatingAfterSweepUnavailable();
        FinishedUnavailable();
        UnknownUnavailable();
        ActivatingExactSweepAvailable();
    }

    private static void IdleAlwaysAvailable()
    {
        var timing = new TeleportTimingSnapshot { StateCode = 0, CountdownDuration = 3f, TeleportWait = 0.5f };
        var eval = TeleportAvailabilityPolicy.Evaluate(timing, 100f);
        AssertEx.True(eval.Available);
        AssertEx.FloatEqual(3.5f, eval.IncrementalCost);
        Program.RecordPass();
    }

    private static void ActivatingBeforeSweepAvailable()
    {
        var timing = new TeleportTimingSnapshot { StateCode = 1, CountdownSecondsLeft = 3, TeleportWait = 0.5f };
        var eval = TeleportAvailabilityPolicy.Evaluate(timing, 2f);
        AssertEx.True(eval.Available);
        AssertEx.FloatEqual(1.5f, eval.IncrementalCost);
        Program.RecordPass();
    }

    private static void ActivatingAfterSweepUnavailable()
    {
        var timing = new TeleportTimingSnapshot { StateCode = 1, CountdownSecondsLeft = 1, TeleportWait = 0.5f };
        AssertEx.False(TeleportAvailabilityPolicy.Evaluate(timing, 3f).Available);
        Program.RecordPass();
    }

    private static void FinishedUnavailable()
    {
        var timing = new TeleportTimingSnapshot { StateCode = 2, CountdownDuration = 3f, TeleportWait = 0.5f };
        AssertEx.False(TeleportAvailabilityPolicy.Evaluate(timing, 0f).Available);
        Program.RecordPass();
    }

    private static void UnknownUnavailable()
    {
        var timing = new TeleportTimingSnapshot { StateCode = 99, CountdownDuration = 3f, TeleportWait = 0.5f };
        AssertEx.False(TeleportAvailabilityPolicy.Evaluate(timing, 0f).Available);
        Program.RecordPass();
    }

    private static void ActivatingExactSweepAvailable()
    {
        var timing = new TeleportTimingSnapshot { StateCode = 1, CountdownSecondsLeft = 2, TeleportWait = 0.5f };
        var eval = TeleportAvailabilityPolicy.Evaluate(timing, 2.5f);
        AssertEx.True(eval.Available);
        AssertEx.FloatEqual(0f, eval.IncrementalCost);
        Program.RecordPass();
    }
}
