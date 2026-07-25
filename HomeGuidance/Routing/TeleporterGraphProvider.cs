using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using HomeGuidance.Runtime;

namespace HomeGuidance.Routing;

public sealed class TeleporterGraphProvider
{
    private readonly GuidanceController _controller;
    private List<TeleporterSnapshot> _snapshots = new();
    private int _lastTopologyHash;
    private int _lastCostHash;

    public TeleporterGraphProvider(GuidanceController controller)
    {
        _controller = controller;
    }

    public IReadOnlyList<TeleporterSnapshot> GetSnapshots() => _snapshots;

    public (int topologyHash, int costHash) GetHashes() => (_lastTopologyHash, _lastCostHash);

    public void Refresh()
    {
        var circles = YAPYAP.TeleportDeadEndCircle.FindObjectsByType<YAPYAP.TeleportDeadEndCircle>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        var newSnapshots = new List<TeleporterSnapshot>();
        var idMap = new Dictionary<YAPYAP.TeleportDeadEndCircle, int>();

        // Assign stable IDs
        foreach (var c in circles)
        {
            if (c == null) continue;
            idMap[c] = c.GetInstanceID();
        }

        foreach (var c in circles)
        {
            if (c == null) continue;

            YAPYAP.TeleportDeadEndCircle paired = null;
            bool hasPaired = TeleporterAccess.TryReadPaired(c, out paired);
            int pairedId = hasPaired && paired != null ? paired.GetInstanceID() : 0;

            TeleporterAccess.TryReadExtractionMode(c, out bool isExtraction);
            TeleporterAccess.TryReadCountdown(c, out int countdown);
            TeleporterAccess.TryReadStateCode(c, out int stateCode);

            float cd = Plugin.ModConfig.TeleportCountdownSeconds.Value;
            float wt = Plugin.ModConfig.TeleportWaitSeconds.Value;
            TeleporterAccess.TryReadPrefabTiming(c, out cd, out wt);

            int extId = 0;
            var extraction = YAPYAP.TeleportExtractionCircle.FindFirstObjectByType<YAPYAP.TeleportExtractionCircle>(
                FindObjectsInactive.Exclude);
            if (extraction != null) extId = extraction.GetInstanceID();

            newSnapshots.Add(new TeleporterSnapshot
            {
                StableId = c.GetInstanceID(),
                SourcePosition = c.transform.position,
                PairedStableId = pairedId,
                HasPaired = hasPaired && paired != null,
                IsInExtractionMode = isExtraction,
                StateCode = stateCode,
                CountdownSecondsLeft = countdown,
                CountdownDuration = cd,
                TeleportWait = wt,
                ExtractionInstanceId = extId
            });
        }

        _snapshots = newSnapshots;

        // Compute hashes
        int topoHash = 0;
        foreach (var s in _snapshots)
        {
            topoHash ^= s.StableId ^ (s.PairedStableId << 1) ^ (s.IsInExtractionMode ? (1 << 16) : 0)
                ^ (s.HasPaired ? (1 << 17) : 0) ^ (s.ExtractionInstanceId << 8);
        }
        // Quantize position
        foreach (var s in _snapshots)
            topoHash ^= ((int)(s.SourcePosition.x * 4f)) ^ ((int)(s.SourcePosition.z * 4f) << 8);

        int costHash = 0;
        foreach (var s in _snapshots)
            costHash ^= s.StateCode ^ (s.CountdownSecondsLeft << 4);

        if (topoHash != _lastTopologyHash)
        {
            _lastTopologyHash = topoHash;
            _controller.MarkDirty(GuidanceDirtyFlags.GraphTopologyChanged);
        }
        if (costHash != _lastCostHash)
        {
            _lastCostHash = costHash;
            _controller.MarkDirty(GuidanceDirtyFlags.TeleportCostChanged);
        }
    }
}
