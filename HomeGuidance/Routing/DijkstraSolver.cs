using System.Collections.Generic;

namespace HomeGuidance.Routing;

/// <summary>
/// Pure C# — no Unity, no BepInEx, no Harmony.
/// Linkable into HomeGuidance.Tests.
/// </summary>
public static class DijkstraSolver
{
    public static RouteSolution Solve(
        IReadOnlyList<RouteNode> nodes,
        IReadOnlyList<RouteEdge> edges,
        int startId,
        int goalId)
    {
        int n = nodes.Count;
        var dist = new float[n];
        var previousEdge = new RouteEdge[n];
        var visited = new bool[n];

        for (int i = 0; i < n; i++) dist[i] = float.MaxValue;
        dist[startId] = 0f;

        var queue = new SortedSet<(float, int)>(Comparer<(float, int)>.Create((a, b) =>
        {
            int cmp = a.Item1.CompareTo(b.Item1);
            return cmp != 0 ? cmp : a.Item2.CompareTo(b.Item2);
        }));
        queue.Add((0f, startId));

        var adjacency = new List<RouteEdge>[n];
        for (int i = 0; i < n; i++) adjacency[i] = new List<RouteEdge>();
        foreach (var edge in edges)
        {
            if (edge.FromId >= 0 && edge.FromId < n)
                adjacency[edge.FromId].Add(edge);
        }

        while (queue.Count > 0)
        {
            var min = queue.Min;
            queue.Remove(min);
            float d = min.Item1;
            int u = min.Item2;

            if (visited[u]) continue;
            visited[u] = true;
            if (u == goalId) break;

            foreach (var edge in adjacency[u])
            {
                float edgeCost;

                if (edge.Type == RouteEdgeType.Walk)
                    edgeCost = edge.WalkCostSeconds;
                else if (edge.Type == RouteEdgeType.LocalTransition)
                    edgeCost = 0f;
                else // Teleport
                {
                    var eval = TeleportAvailabilityPolicy.Evaluate(edge.TeleportTiming, d);
                    if (!eval.Available) continue;
                    edgeCost = eval.IncrementalCost;
                }

                float alt = d + edgeCost;
                int v = edge.ToId;
                const float epsilon = 1e-4f;

                if (alt < dist[v] - epsilon)
                {
                    dist[v] = alt;
                    previousEdge[v] = edge;
                    queue.Add((alt, v));
                }
            }
        }

        if (!visited[goalId])
            return new RouteSolution { Reachable = false };

        // Backtrack
        var resultEdges = new List<RouteEdge>();
        int cur = goalId;
        while (cur != startId)
        {
            var edge = previousEdge[cur];
            if (edge == null) break;
            resultEdges.Add(edge);
            cur = edge.FromId;
        }
        resultEdges.Reverse();

        return new RouteSolution
        {
            Reachable = true,
            TotalCostSeconds = dist[goalId],
            Edges = resultEdges
        };
    }
}
