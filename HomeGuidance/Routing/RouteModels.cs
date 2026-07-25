using System.Collections.Generic;
using UnityEngine;

namespace HomeGuidance.Routing;

public enum RouteNodeKind { Start, Extraction, TeleporterIn, TeleporterOut }

public enum RouteEdgeType { Walk, Teleport, LocalTransition }

public enum RouteReplanReason
{
    Initial,
    Periodic,
    PlayerTeleported,
    RouteDeviation,
    TargetChanged,
    GraphChanged,
    ConfigChanged,
    ExtractionChanged
}

public readonly struct RouteNode
{
    public int Id { get; init; }
    public RouteNodeKind Kind { get; init; }
    public Vector3 Position { get; init; }
    public int StableObjectId { get; init; }
}

public readonly struct TeleportTimingSnapshot
{
    public int StateCode { get; init; }
    public int CountdownSecondsLeft { get; init; }
    public float CountdownDuration { get; init; }
    public float TeleportWait { get; init; }
}

public sealed class RouteEdge
{
    public int FromId;
    public int ToId;
    public RouteEdgeType Type;
    public float WalkCostSeconds; // Walk only; LocalTransition is 0
    public Vector3[] WalkCorners;
    public int TeleporterStableId;
    public TeleportTimingSnapshot TeleportTiming; // Teleport only
}

public sealed class RouteSolution
{
    public bool Reachable;
    public float TotalCostSeconds;
    public List<RouteEdge> Edges;
}

/// <summary>
/// Frozen display segment for trail/arrow rendering.
/// Immutable after plan submission.
/// </summary>
public sealed class DisplayWalkSegment
{
    /// <summary>Cleaned and joined Walk corners. Null if MarkerOnly.</summary>
    public Vector3[] Corners { get; init; }

    /// <summary>The current sub-target position (next waypoint or teleport entrance).</summary>
    public Vector3 SubTarget { get; init; }

    public bool EndsAtTeleport { get; init; }
    public int TeleporterStableId { get; init; }

    public static DisplayWalkSegment MarkerOnly(Vector3 entrance, int teleporterId)
    {
        return new DisplayWalkSegment
        {
            Corners = null,
            SubTarget = entrance,
            EndsAtTeleport = true,
            TeleporterStableId = teleporterId
        };
    }
}

public sealed class RouteRequest
{
    public int RoundToken;
    public Vector3 ActualPlayerPosition;
    public Vector3 SampledStart;
    public Vector3 ExtractionVisualPosition;
    public int ExtractionInstanceId;
    public IReadOnlyList<TeleporterSnapshot> Teleporters;
    public float EstimatedWalkSpeed;
    public float TeleportCountdownSeconds;
    public float TeleportWaitSeconds;
    public float Now;
    public RouteReplanReason Reason;
}

public sealed class RoutePlan
{
    public bool IsValid;
    public float TotalCostSeconds;
    public List<RouteEdge> Edges;
    public int CurrentEdgeIndex;
    public DisplayWalkSegment DisplaySegment;
    public int TopologySignature;
    public int RoundToken;
    public int TeleportGeneration;
    public Vector3 PlannedFromPosition;
    public float PlannedAtTime;
    public RouteReplanReason Reason;

    public Vector3 CurrentSubTarget => DisplaySegment?.SubTarget ?? Vector3.zero;
    public bool CurrentSubTargetIsTeleportEntrance => DisplaySegment?.EndsAtTeleport ?? false;
    public Vector3[] CurrentWalkCorners => DisplaySegment?.Corners;
}
