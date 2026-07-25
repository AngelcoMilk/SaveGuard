using System;
using System.Collections.Generic;
using UnityEngine;
using HomeGuidance.Runtime;

namespace HomeGuidance.Routing;

public sealed class RoutePlanner
{
    private readonly GuidanceController _controller;
    private readonly NavMeshPathCache _cache = new();

    public RoutePlanner(GuidanceController controller)
    {
        _controller = controller;
    }

    public void Clear()
    {
        _cache.OnNewRound(_controller.RoundGuidanceState.CurrentRoundToken);
    }

    public void ClearStartEdges()
    {
        // Dynamic start edges are not stored long-term so no explicit clear needed.
        // The next ComputePlan will rebuild S-node edges fresh.
    }

    public RoutePlan ComputePlan(
        RouteRequest request,
        RoutePlan currentPlan,
        int teleportGeneration,
        int roundToken)
    {
        if (request.Teleporters == null || request.Teleporters.Count == 0)
        {
            // No teleporters — simple Walk route
            return ComputeSimpleWalkPlan(request, roundToken, teleportGeneration);
        }

        return ComputeFullPlan(request, currentPlan, teleportGeneration, roundToken);
    }

    private RoutePlan ComputeSimpleWalkPlan(RouteRequest request, int roundToken, int teleportGeneration)
    {
        // Sample start and extraction
        Vector3 sPos = request.ActualPlayerPosition;
        Vector3 ePos = request.ExtractionVisualPosition;

        NavMeshPathService.TrySample(sPos, 2f, out sPos);
        NavMeshPathService.TrySample(ePos, 2f, out ePos);

        if (!NavMeshPathService.TryCalculateCompletePath(sPos, ePos, out var corners, out var length))
            return null;

        float cost = length / Mathf.Max(0.1f, request.EstimatedWalkSpeed);

        var plan = new RoutePlan
        {
            IsValid = true,
            TotalCostSeconds = cost,
            Edges = new List<RouteEdge>
            {
                new RouteEdge
                {
                    FromId = 0, ToId = 1, Type = RouteEdgeType.Walk,
                    WalkCostSeconds = cost, WalkCorners = corners
                }
            },
            CurrentEdgeIndex = 0,
            TopologySignature = 0,
            RoundToken = roundToken,
            TeleportGeneration = teleportGeneration,
            PlannedFromPosition = request.ActualPlayerPosition,
            PlannedAtTime = request.Now,
            Reason = request.Reason
        };

        plan.DisplaySegment = BuildDisplayWalkSegment(plan);
        return plan;
    }

    private RoutePlan ComputeFullPlan(
        RouteRequest request,
        RoutePlan currentPlan,
        int teleportGeneration,
        int roundToken)
    {
        // ── Build graph ──
        var graph = BuildGraph(request);
        if (graph.nodes.Count == 0) return null;

        // ── Run Dijkstra ──
        int startId = 0; // S is always node 0
        int goalId = 1;  // E is always node 1
        var solution = DijkstraSolver.Solve(graph.nodes, graph.edges, startId, goalId);

        if (!solution.Reachable || solution.Edges == null || solution.Edges.Count == 0)
            return null;

        // ── Build RoutePlan ──
        float totalCost = solution.TotalCostSeconds;
        int topoHash = ComputeTopologySignature(solution.Edges);

        var plan = new RoutePlan
        {
            IsValid = true,
            TotalCostSeconds = totalCost,
            Edges = solution.Edges,
            CurrentEdgeIndex = 0,
            TopologySignature = topoHash,
            RoundToken = roundToken,
            TeleportGeneration = teleportGeneration,
            PlannedFromPosition = request.ActualPlayerPosition,
            PlannedAtTime = request.Now,
            Reason = request.Reason
        };

        plan.DisplaySegment = BuildDisplayWalkSegment(plan);

        if (Plugin.ModConfig.DebugLogging.Value)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("Route plan: ");
            foreach (var e in solution.Edges)
            {
                sb.Append(e.Type == RouteEdgeType.LocalTransition ? "Local→" :
                    e.Type == RouteEdgeType.Teleport ? $"Teleport#{e.TeleporterStableId}→" : "Walk→");
            }
            GuidanceLog.Debug(sb.ToString());
        }

        return plan;
    }

    private GraphData BuildGraph(RouteRequest request)
    {
        var graph = new GraphData();
        float walkSpeed = Mathf.Max(0.1f, request.EstimatedWalkSpeed);

        // Sample S and E
        Vector3 sSample = request.ActualPlayerPosition;
        Vector3 eSample = request.ExtractionVisualPosition;
        NavMeshPathService.TrySample(sSample, 2f, out sSample);
        NavMeshPathService.TrySample(eSample, 2f, out eSample);

        // Add S (0) and E (1)
        graph.AddNode(new RouteNode { Id = 0, Kind = RouteNodeKind.Start, Position = sSample });
        graph.AddNode(new RouteNode { Id = 1, Kind = RouteNodeKind.Extraction, Position = eSample });

        // ── Pass 1: Create T.in / T.out for all valid teleporters ──
        var teleporterData = new List<(TeleporterSnapshot snapshot, int inId, int outId)>();

        foreach (var t in request.Teleporters)
        {
            Vector3 tpSample = t.SourcePosition;
            if (!NavMeshPathService.TrySample(tpSample, 2f, out tpSample))
            {
                GuidanceLog.Debug($"Teleporter {t.StableId}: NavMesh sample failed, skipping");
                continue;
            }

            int inId = graph.NextId();
            int outId = graph.NextId();

            graph.AddNode(new RouteNode { Id = inId, Kind = RouteNodeKind.TeleporterIn, Position = tpSample, StableObjectId = t.StableId });
            graph.AddNode(new RouteNode { Id = outId, Kind = RouteNodeKind.TeleporterOut, Position = tpSample, StableObjectId = t.StableId });

            // LocalTransition: T.out → T.in (zero cost, non-renderable)
            graph.AddEdge(new RouteEdge
            {
                FromId = outId, ToId = inId, Type = RouteEdgeType.LocalTransition,
                WalkCostSeconds = 0f, TeleporterStableId = t.StableId
            });

            teleporterData.Add((t, inId, outId));
        }

        // ── Pass 2: Resolve teleport targets after all out nodes exist ──
        var idToOutNode = new Dictionary<int, (int outId, int inId)>();
        foreach (var td in teleporterData)
            idToOutNode[td.snapshot.StableId] = (td.outId, td.inId);

        foreach (var td in teleporterData)
        {
            int targetId = -1;
            if (td.snapshot.IsInExtractionMode)
            {
                targetId = 1; // E
            }
            else if (td.snapshot.HasPaired && idToOutNode.ContainsKey(td.snapshot.PairedStableId))
            {
                targetId = idToOutNode[td.snapshot.PairedStableId].outId;
            }

            if (targetId >= 0)
            {
                var timing = new TeleportTimingSnapshot
                {
                    StateCode = td.snapshot.StateCode,
                    CountdownSecondsLeft = td.snapshot.CountdownSecondsLeft,
                    CountdownDuration = td.snapshot.CountdownDuration,
                    TeleportWait = td.snapshot.TeleportWait
                };

                graph.AddEdge(new RouteEdge
                {
                    FromId = td.inId, ToId = targetId, Type = RouteEdgeType.Teleport,
                    TeleporterStableId = td.snapshot.StableId,
                    TeleportTiming = timing
                });
            }
        }

        // ── Pass 3a: Walk edges between S, E, all T.in ──
        var walkNodes = new List<RouteNode> { graph.nodes[0] }; // S
        for (int i = 1; i < graph.nodes.Count; i++)
        {
            var node = graph.nodes[i];
            if (node.Kind == RouteNodeKind.Extraction || node.Kind == RouteNodeKind.TeleporterIn)
                walkNodes.Add(node);
        }

        for (int i = 0; i < walkNodes.Count; i++)
        {
            for (int j = 0; j < walkNodes.Count; j++)
            {
                if (i == j) continue;
                var a = walkNodes[i];
                var b = walkNodes[j];

                if (NavMeshPathService.TryCalculateCompletePath(a.Position, b.Position, out var corners, out var length))
                {
                    graph.AddEdge(new RouteEdge
                    {
                        FromId = a.Id, ToId = b.Id, Type = RouteEdgeType.Walk,
                        WalkCostSeconds = length / walkSpeed, WalkCorners = corners
                    });
                }
            }
        }

        return graph;
    }

    public static DisplayWalkSegment BuildDisplayWalkSegment(RoutePlan plan)
    {
        if (plan == null || plan.Edges == null || plan.Edges.Count == 0)
            return null;

        int i = plan.CurrentEdgeIndex;
        if (i >= plan.Edges.Count) return null;

        // Skip LocalTransition edges
        while (i < plan.Edges.Count && plan.Edges[i].Type == RouteEdgeType.LocalTransition)
            i++;

        if (i >= plan.Edges.Count) return null;

        var edge = plan.Edges[i];

        // Teleport as first visible edge → MarkerOnly
        if (edge.Type == RouteEdgeType.Teleport)
        {
            var tpNode = FindNodeByEdgeSource(plan, edge);
            return DisplayWalkSegment.MarkerOnly(tpNode?.Position ?? Vector3.zero, edge.TeleporterStableId);
        }

        // Not a Walk → nothing to show
        if (edge.Type != RouteEdgeType.Walk) return null;

        // Collect consecutive Walk edges
        var cornersList = new List<Vector3>();
        while (i < plan.Edges.Count && plan.Edges[i].Type == RouteEdgeType.Walk)
        {
            AppendCornersRemoveDuplicateJoin(cornersList, plan.Edges[i].WalkCorners);
            i++;
        }

        // Check if next is Teleport
        bool endsAtTeleport = false;
        Vector3 entrance = Vector3.zero;
        int tpId = 0;

        // Skip LocalTransition after Walk
        while (i < plan.Edges.Count && plan.Edges[i].Type == RouteEdgeType.LocalTransition)
            i++;

        if (i < plan.Edges.Count && plan.Edges[i].Type == RouteEdgeType.Teleport)
        {
            endsAtTeleport = true;
            tpId = plan.Edges[i].TeleporterStableId;
            var tpNode = FindNodeByEdgeSource(plan, plan.Edges[i]);
            entrance = tpNode?.Position ?? Vector3.zero;
        }

        return new DisplayWalkSegment
        {
            Corners = cornersList.ToArray(),
            SubTarget = endsAtTeleport ? entrance : (cornersList.Count > 0 ? cornersList[cornersList.Count - 1] : Vector3.zero),
            EndsAtTeleport = endsAtTeleport,
            TeleporterStableId = tpId
        };
    }

    private static void AppendCornersRemoveDuplicateJoin(List<Vector3> list, Vector3[] newCorners)
    {
        if (newCorners == null || newCorners.Length == 0) return;

        int start = 0;
        if (list.Count > 0 && newCorners.Length > 0)
        {
            if (Vector3.Distance(list[list.Count - 1], newCorners[0]) < 0.1f)
                start = 1;
        }

        for (int i = start; i < newCorners.Length; i++)
            list.Add(newCorners[i]);
    }

    private static RouteNode? FindNodeByEdgeSource(RoutePlan plan, RouteEdge edge)
    {
        // We don't store the full node list in RoutePlan, so approximate.
        // For teleport entrance, use the Walk edge's last corner.
        if (plan.CurrentWalkCorners != null && plan.CurrentWalkCorners.Length > 0)
            return new RouteNode { Position = plan.CurrentWalkCorners[plan.CurrentWalkCorners.Length - 1] };
        return null;
    }

    private static int ComputeTopologySignature(List<RouteEdge> edges)
    {
        int hash = 0;
        foreach (var e in edges)
        {
            hash ^= (int)e.Type << 24;
            hash ^= e.FromId ^ (e.ToId << 8);
            if (e.Type == RouteEdgeType.Teleport)
                hash ^= e.TeleporterStableId;
        }
        return hash;
    }

    private sealed class GraphData
    {
        public readonly List<RouteNode> nodes = new();
        public readonly List<RouteEdge> edges = new();
        private int _nextId;

        public int NextId() => _nextId++;
        public void AddNode(RouteNode node) => nodes.Add(node);
        public void AddEdge(RouteEdge edge) => edges.Add(edge);
    }
}
