using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HomeGuidance.Arrival;

public sealed class ArrivalTracker
{
    private float _nextScanTime;

    /// <summary>
    /// Scans all visible players and returns candidate netIds in range.
    /// Does NOT modify RoundGuidanceState — Controller handles commit.
    /// </summary>
    public ArrivalScanResult Scan(
        float now,
        int capturedRoundToken,
        IReadOnlyCollection<uint> alreadyReached,
        YAPYAP.TeleportExtractionCircle extraction,
        float arrivalRadius)
    {
        if (now < _nextScanTime)
            return ArrivalScanResult.Empty(capturedRoundToken);

        _nextScanTime = now; // caller manages interval

        var gm = YAPYAP.GameManager.Instance;
        if (gm == null || !gm.RoundActive || extraction == null)
            return ArrivalScanResult.Empty(capturedRoundToken);

        float radiusSq = arrivalRadius * arrivalRadius;
        var candidates = new List<uint>();
        var extPos = extraction.transform.position;

        foreach (var pawn in gm.playersByPlayerId.Values)
        {
            if (pawn == null || pawn.netId == 0) continue;
            if (pawn.IsDead || pawn.IsExtracted) continue;
            if (alreadyReached.Contains(pawn.netId)) continue;

            var delta = pawn.transform.position - extPos;
            if (delta.sqrMagnitude <= radiusSq)
                candidates.Add(pawn.netId);
        }

        return new ArrivalScanResult(capturedRoundToken, candidates);
    }
}
