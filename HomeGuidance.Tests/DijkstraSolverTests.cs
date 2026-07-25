using System;
using System.Collections.Generic;

namespace HomeGuidance.Tests;

public static class DijkstraSolverTests
{
    public static void RunAll()
    {
        SingleWalkPath();
        UnreachableGoal();
        DirectedEdgeCannotReverse();
        CyclicTeleporterRing();
        ChainedTeleportWithLocalTransition();
        WalkVsTeleportTimeChoice();
        ActivatingCostDecreases();
        FinishedEdgeNotAvailable();
        ActivatingExactSweepAvailable();
    }

    private static void SingleWalkPath()
    {
        var nodes = new List<TestRouteNode>
        {
            new() { Id = 0, Kind = RouteNodeKind.Start },
            new() { Id = 1, Kind = RouteNodeKind.Extraction }
        };
        var edges = new List<TestRouteEdge>
        {
            new() { FromId = 0, ToId = 1, Type = RouteEdgeType.Walk, WalkCostSeconds = 10f }
        };
        var sol = DijkstraSolver.Solve(nodes, edges, 0, 1);
        AssertEx.True(sol.Reachable);
        AssertEx.FloatEqual(10f, sol.TotalCostSeconds);
        Program.RecordPass();
    }

    private static void UnreachableGoal()
    {
        var nodes = new List<TestRouteNode> { new() { Id = 0 }, new() { Id = 1 } };
        var sol = DijkstraSolver.Solve(nodes, new List<TestRouteEdge>(), 0, 1);
        AssertEx.False(sol.Reachable);
        Program.RecordPass();
    }

    private static void DirectedEdgeCannotReverse()
    {
        var nodes = new List<TestRouteNode>
        {
            new() { Id = 0 }, new() { Id = 1 }, new() { Id = 2 }
        };
        var edges = new List<TestRouteEdge>
        {
            new() { FromId = 0, ToId = 1, Type = RouteEdgeType.Walk, WalkCostSeconds = 5f },
            new() { FromId = 1, ToId = 2, Type = RouteEdgeType.Walk, WalkCostSeconds = 5f }
        };
        AssertEx.True(DijkstraSolver.Solve(nodes, edges, 0, 2).Reachable);
        AssertEx.False(DijkstraSolver.Solve(nodes, edges, 2, 0).Reachable);
        Program.RecordPass();
    }

    private static void CyclicTeleporterRing()
    {
        var nodes = new List<TestRouteNode>
        {
            new() { Id = 0, Kind = RouteNodeKind.Start },
            new() { Id = 1, Kind = RouteNodeKind.Extraction },
            new() { Id = 2, Kind = RouteNodeKind.TeleporterIn },
            new() { Id = 3, Kind = RouteNodeKind.TeleporterOut },
            new() { Id = 4, Kind = RouteNodeKind.TeleporterIn },
            new() { Id = 5, Kind = RouteNodeKind.TeleporterOut },
            new() { Id = 6, Kind = RouteNodeKind.TeleporterIn },
            new() { Id = 7, Kind = RouteNodeKind.TeleporterOut },
        };
        var idleTiming = new TeleportTimingSnapshot
        { StateCode = 0, CountdownDuration = 3f, TeleportWait = 0.5f };
        var edges = new List<TestRouteEdge>
        {
            new() { FromId = 3, ToId = 2, Type = RouteEdgeType.LocalTransition },
            new() { FromId = 5, ToId = 4, Type = RouteEdgeType.LocalTransition },
            new() { FromId = 7, ToId = 6, Type = RouteEdgeType.LocalTransition },
            new() { FromId = 2, ToId = 5, Type = RouteEdgeType.Teleport, TeleportTiming = idleTiming },
            new() { FromId = 4, ToId = 7, Type = RouteEdgeType.Teleport, TeleportTiming = idleTiming },
            new() { FromId = 6, ToId = 3, Type = RouteEdgeType.Teleport, TeleportTiming = idleTiming },
            new() { FromId = 0, ToId = 2, Type = RouteEdgeType.Walk, WalkCostSeconds = 5f },
            new() { FromId = 7, ToId = 1, Type = RouteEdgeType.Walk, WalkCostSeconds = 5f },
            new() { FromId = 0, ToId = 4, Type = RouteEdgeType.Walk, WalkCostSeconds = 8f },
            new() { FromId = 5, ToId = 6, Type = RouteEdgeType.Walk, WalkCostSeconds = 1f },
            new() { FromId = 3, ToId = 1, Type = RouteEdgeType.Walk, WalkCostSeconds = 8f },
        };
        AssertEx.True(DijkstraSolver.Solve(nodes, edges, 0, 1).Reachable);
        Program.RecordPass();
    }

    private static void ChainedTeleportWithLocalTransition()
    {
        var nodes = new List<TestRouteNode>
        {
            new() { Id = 0, Kind = RouteNodeKind.Start },
            new() { Id = 1, Kind = RouteNodeKind.Extraction },
            new() { Id = 2, Kind = RouteNodeKind.TeleporterIn },
            new() { Id = 3, Kind = RouteNodeKind.TeleporterOut },
            new() { Id = 4, Kind = RouteNodeKind.TeleporterIn },
            new() { Id = 5, Kind = RouteNodeKind.TeleporterOut },
        };
        var idleTiming = new TeleportTimingSnapshot
        { StateCode = 0, CountdownDuration = 3f, TeleportWait = 0.5f };
        var edges = new List<TestRouteEdge>
        {
            new() { FromId = 3, ToId = 2, Type = RouteEdgeType.LocalTransition },
            new() { FromId = 5, ToId = 4, Type = RouteEdgeType.LocalTransition },
            new() { FromId = 2, ToId = 5, Type = RouteEdgeType.Teleport, TeleportTiming = idleTiming },
            new() { FromId = 0, ToId = 2, Type = RouteEdgeType.Walk, WalkCostSeconds = 5f },
            new() { FromId = 4, ToId = 1, Type = RouteEdgeType.Walk, WalkCostSeconds = 5f },
        };
        var sol = DijkstraSolver.Solve(nodes, edges, 0, 1);
        AssertEx.True(sol.Reachable);
        AssertEx.FloatEqual(13.5f, sol.TotalCostSeconds);
        Program.RecordPass();
    }

    private static void WalkVsTeleportTimeChoice()
    {
        var nodes = new List<TestRouteNode>
        {
            new() { Id = 0 }, new() { Id = 1 },
            new() { Id = 2, Kind = RouteNodeKind.TeleporterIn },
            new() { Id = 3, Kind = RouteNodeKind.TeleporterOut },
        };
        var idleTiming = new TeleportTimingSnapshot
        { StateCode = 0, CountdownDuration = 3f, TeleportWait = 0.5f };
        var edges = new List<TestRouteEdge>
        {
            new() { FromId = 3, ToId = 2, Type = RouteEdgeType.LocalTransition },
            new() { FromId = 0, ToId = 1, Type = RouteEdgeType.Walk, WalkCostSeconds = 20f },
            new() { FromId = 0, ToId = 2, Type = RouteEdgeType.Walk, WalkCostSeconds = 3f },
            new() { FromId = 2, ToId = 3, Type = RouteEdgeType.Teleport, TeleportTiming = idleTiming },
            new() { FromId = 3, ToId = 1, Type = RouteEdgeType.Walk, WalkCostSeconds = 2f },
        };
        var sol = DijkstraSolver.Solve(nodes, edges, 0, 1);
        AssertEx.True(sol.Reachable);
        AssertEx.True(sol.TotalCostSeconds < 15f, $"Expected <15s, got {sol.TotalCostSeconds}");
        Program.RecordPass();
    }

    private static void ActivatingCostDecreases()
    {
        var timing = new TeleportTimingSnapshot
        { StateCode = 1, CountdownSecondsLeft = 2, CountdownDuration = 3f, TeleportWait = 0.5f };
        var eval = TeleportAvailabilityPolicy.Evaluate(timing, 5f);
        AssertEx.False(eval.Available);
        eval = TeleportAvailabilityPolicy.Evaluate(timing, 1.5f);
        AssertEx.True(eval.Available);
        AssertEx.FloatEqual(1.0f, eval.IncrementalCost);
        Program.RecordPass();
    }

    private static void FinishedEdgeNotAvailable()
    {
        var timing = new TeleportTimingSnapshot
        { StateCode = 2, CountdownDuration = 3f, TeleportWait = 0.5f };
        AssertEx.False(TeleportAvailabilityPolicy.Evaluate(timing, 0f).Available);
        Program.RecordPass();
    }

    private static void ActivatingExactSweepAvailable()
    {
        var timing = new TeleportTimingSnapshot
        { StateCode = 1, CountdownSecondsLeft = 2, TeleportWait = 0.5f };
        var eval = TeleportAvailabilityPolicy.Evaluate(timing, 2.5f);
        AssertEx.True(eval.Available);
        AssertEx.FloatEqual(0f, eval.IncrementalCost);
        Program.RecordPass();
    }
}
