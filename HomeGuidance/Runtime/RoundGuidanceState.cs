using System.Collections.Generic;

namespace HomeGuidance.Runtime;

public sealed class RoundGuidanceState
{
    public bool RoundActive { get; private set; }
    public int CurrentRoundToken { get; private set; }
    public bool GuidanceUnlocked { get; private set; }

    private readonly HashSet<uint> _reachedNetIds = new();
    public IReadOnlyCollection<uint> ReachedPlayerNetIds => _reachedNetIds;

    public bool BeginRound(int roundToken)
    {
        // Same token in active round: strict no-op
        if (RoundActive && CurrentRoundToken == roundToken)
            return false;

        _reachedNetIds.Clear();
        GuidanceUnlocked = false;
        RoundActive = true;
        CurrentRoundToken = roundToken;
        return true;
    }

    public bool MarkReached(uint netId, int roundToken)
    {
        if (!RoundActive || roundToken != CurrentRoundToken)
            return false;
        if (!_reachedNetIds.Add(netId))
            return false;

        if (!GuidanceUnlocked)
            GuidanceUnlocked = true;

        return true;
    }

    public bool HasReached(uint netId)
    {
        return _reachedNetIds.Contains(netId);
    }

    public bool EndRound()
    {
        if (!RoundActive) return false;
        RoundActive = false;
        return true;
    }
}
