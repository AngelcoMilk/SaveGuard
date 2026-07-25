namespace HomeGuidance.Tests;

public static class RouteSelectionPolicyTests
{
    public static void RunAll()
    {
        NullCandidateNotReplaced();
        InvalidCurrentReplaced();
        TeleportedForcesReplace();
        DeviationForcesReplace();
        SameTopologyRefreshes();
        SmallGainNotReplaced();
        LargeGainReplaced();
        DifferentGenerationReplaced();
    }

    private static TestRoutePlan MakePlan(float cost, int topoSig = 1, int round = 1, int gen = 1,
        RouteReplanReason reason = RouteReplanReason.Periodic)
    {
        return new TestRoutePlan { IsValid = true, TotalCostSeconds = cost, TopologySignature = topoSig, RoundToken = round, TeleportGeneration = gen, Reason = reason };
    }

    private static void NullCandidateNotReplaced()
    {
        AssertEx.False(RouteSelectionPolicy.ShouldReplace(MakePlan(10f), null, 0.35f));
        Program.RecordPass();
    }

    private static void InvalidCurrentReplaced()
    {
        AssertEx.True(RouteSelectionPolicy.ShouldReplace(null, MakePlan(10f), 0.35f));
        Program.RecordPass();
    }

    private static void TeleportedForcesReplace()
    {
        AssertEx.True(RouteSelectionPolicy.ShouldReplace(MakePlan(10f), MakePlan(10f, 1, 1, 2, RouteReplanReason.PlayerTeleported), 0.35f));
        Program.RecordPass();
    }

    private static void DeviationForcesReplace()
    {
        AssertEx.True(RouteSelectionPolicy.ShouldReplace(MakePlan(10f), MakePlan(10f, 2, 1, 1, RouteReplanReason.RouteDeviation), 0.35f));
        Program.RecordPass();
    }

    private static void SameTopologyRefreshes()
    {
        AssertEx.True(RouteSelectionPolicy.ShouldReplace(MakePlan(10f, 5), MakePlan(9.5f, 5), 0.35f));
        Program.RecordPass();
    }

    private static void SmallGainNotReplaced()
    {
        AssertEx.False(RouteSelectionPolicy.ShouldReplace(MakePlan(10f, 1), MakePlan(9.8f, 2), 0.35f));
        Program.RecordPass();
    }

    private static void LargeGainReplaced()
    {
        AssertEx.True(RouteSelectionPolicy.ShouldReplace(MakePlan(10f, 1), MakePlan(9.4f, 2), 0.35f));
        Program.RecordPass();
    }

    private static void DifferentGenerationReplaced()
    {
        AssertEx.True(RouteSelectionPolicy.ShouldReplace(MakePlan(10f, 1, 1, 1), MakePlan(10.5f, 1, 2, 2), 0.35f));
        Program.RecordPass();
    }
}
