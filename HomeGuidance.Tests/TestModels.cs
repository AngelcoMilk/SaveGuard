using System.Collections.Generic;

namespace HomeGuidance.Tests;

// Pure-C# copies of RouteModels used for testing without Unity dependency.

public enum RouteNodeKind { Start, Extraction, TeleporterIn, TeleporterOut }
public enum RouteEdgeType { Walk, Teleport, LocalTransition }
public enum RouteReplanReason
{
    Initial, Periodic, PlayerTeleported, RouteDeviation,
    TargetChanged, GraphChanged, ConfigChanged, ExtractionChanged
}

public readonly struct TestRouteNode
{
    public int Id { get; init; }
    public RouteNodeKind Kind { get; init; }
}

public readonly struct TeleportTimingSnapshot
{
    public int StateCode { get; init; }
    public int CountdownSecondsLeft { get; init; }
    public float CountdownDuration { get; init; }
    public float TeleportWait { get; init; }
}

public sealed class TestRouteEdge
{
    public int FromId;
    public int ToId;
    public RouteEdgeType Type;
    public float WalkCostSeconds;
    public int TeleporterStableId;
    public TeleportTimingSnapshot TeleportTiming;
}

public sealed class TestRouteSolution
{
    public bool Reachable;
    public float TotalCostSeconds;
    public List<TestRouteEdge> Edges;
}

public sealed class TestRoutePlan
{
    public bool IsValid;
    public float TotalCostSeconds;
    public int TopologySignature;
    public int RoundToken;
    public int TeleportGeneration;
    public RouteReplanReason Reason;
}
