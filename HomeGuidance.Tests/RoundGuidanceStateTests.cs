namespace HomeGuidance.Tests;

public static class RoundGuidanceStateTests
{
    public static void RunAll()
    {
        FirstReachedUnlocks();
        DuplicateReachedReturnsFalse();
        LeaveDoesNotRemove();
        DisconnectDoesNotRemove();
        EndRoundClears();
        NewRoundTokenClears();
        SameTokenBeginRoundIsNoOp();
        LateMarkWithWrongTokenRejected();
    }

    private static void FirstReachedUnlocks()
    {
        var state = new RoundGuidanceState();
        state.BeginRound(1);
        AssertEx.True(state.RoundActive);
        AssertEx.False(state.GuidanceUnlocked);
        AssertEx.True(state.MarkReached(100u, 1));
        AssertEx.True(state.GuidanceUnlocked);
        AssertEx.True(state.HasReached(100u));
        Program.RecordPass();
    }

    private static void DuplicateReachedReturnsFalse()
    {
        var state = new RoundGuidanceState();
        state.BeginRound(1);
        AssertEx.True(state.MarkReached(100u, 1));
        AssertEx.False(state.MarkReached(100u, 1));
        Program.RecordPass();
    }

    private static void LeaveDoesNotRemove()
    {
        var state = new RoundGuidanceState();
        state.BeginRound(1);
        state.MarkReached(100u, 1);
        AssertEx.True(state.HasReached(100u));
        Program.RecordPass();
    }

    private static void DisconnectDoesNotRemove()
    {
        var state = new RoundGuidanceState();
        state.BeginRound(1);
        state.MarkReached(100u, 1);
        AssertEx.True(state.HasReached(100u));
        Program.RecordPass();
    }

    private static void EndRoundClears()
    {
        var state = new RoundGuidanceState();
        state.BeginRound(1);
        state.MarkReached(100u, 1);
        AssertEx.True(state.EndRound());
        AssertEx.False(state.RoundActive);
        Program.RecordPass();
    }

    private static void NewRoundTokenClears()
    {
        var state = new RoundGuidanceState();
        state.BeginRound(1);
        state.MarkReached(100u, 1);
        AssertEx.True(state.HasReached(100u));
        state.BeginRound(2);
        AssertEx.False(state.HasReached(100u));
        AssertEx.False(state.GuidanceUnlocked);
        Program.RecordPass();
    }

    private static void SameTokenBeginRoundIsNoOp()
    {
        var state = new RoundGuidanceState();
        state.BeginRound(1);
        state.MarkReached(100u, 1);
        AssertEx.True(state.HasReached(100u));
        AssertEx.False(state.BeginRound(1));
        AssertEx.True(state.HasReached(100u));
        Program.RecordPass();
    }

    private static void LateMarkWithWrongTokenRejected()
    {
        var state = new RoundGuidanceState();
        state.BeginRound(1);
        state.BeginRound(2);
        AssertEx.False(state.HasReached(100u));
        AssertEx.False(state.MarkReached(100u, 1));
        AssertEx.False(state.HasReached(100u));
        Program.RecordPass();
    }
}
