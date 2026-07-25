using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HomeGuidance.Routing;

public static class NavMeshPathService
{
    public static bool TrySample(Vector3 visualPoint, float radius, out Vector3 groundPoint)
    {
        if (NavMesh.SamplePosition(visualPoint, out var hit, radius, NavMesh.AllAreas))
        {
            groundPoint = hit.position;
            return true;
        }
        groundPoint = visualPoint;
        return false;
    }

    public static bool TryCalculateCompletePath(Vector3 from, Vector3 to, out Vector3[] corners, out float length)
    {
        corners = null;
        length = 0f;

        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path))
            return false;

        if (path.status != NavMeshPathStatus.PathComplete)
            return false;

        corners = new Vector3[path.corners.Length];
        System.Array.Copy(path.corners, corners, path.corners.Length);

        var list = new List<Vector3>(corners);
        length = PathGeometry.CleanAndMeasure(ref list);
        corners = list.ToArray();

        return corners.Length >= 2;
    }
}
