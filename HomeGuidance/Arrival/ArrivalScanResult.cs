using System.Collections.Generic;
using UnityEngine;

namespace HomeGuidance.Arrival;

/// <summary>
/// Pure scan result — does not modify RoundGuidanceState.
/// Controller commits to state separately.
/// </summary>
public sealed class ArrivalScanResult
{
    public int RoundToken { get; }
    public IReadOnlyList<uint> CandidateNetIds { get; }
    public bool AnyCandidate => CandidateNetIds.Count > 0;

    public ArrivalScanResult(int roundToken, List<uint> candidates)
    {
        RoundToken = roundToken;
        CandidateNetIds = candidates.ToArray();
    }

    public static ArrivalScanResult Empty(int roundToken) => new(roundToken, new List<uint>());
}
