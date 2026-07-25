using System;
using System.Collections.Generic;
using UnityEngine;

namespace HomeGuidance.Routing;

public static class PathGeometry
{
    public static float CleanAndMeasure(ref List<Vector3> corners)
    {
        if (corners == null || corners.Count < 2) return 0f;

        var cleaned = new List<Vector3> { corners[0] };
        float total = 0f;
        for (int i = 1; i < corners.Count; i++)
        {
            float d = Vector3.Distance(cleaned[cleaned.Count - 1], corners[i]);
            if (d > 0.05f)
            {
                cleaned.Add(corners[i]);
                total += d;
            }
        }
        corners = cleaned;
        return total;
    }

    public static float MinDistanceToPolyline(Vector3 point, Vector3[] polyline)
    {
        if (polyline == null || polyline.Length < 2) return float.MaxValue;
        float min = float.MaxValue;
        for (int i = 1; i < polyline.Length; i++)
        {
            float d = DistancePointSegment(point, polyline[i - 1], polyline[i]);
            if (d < min) min = d;
        }
        return min;
    }

    public static float DistancePointSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        Vector3 ap = p - a;
        float t = Vector3.Dot(ap, ab) / ab.sqrMagnitude;
        t = Mathf.Clamp01(t);
        return Vector3.Distance(p, a + t * ab);
    }

    public static Vector3 FindClosestProjection(Vector3 p, Vector3[] polyline, out float cumulativeDistance)
    {
        cumulativeDistance = 0f;
        if (polyline == null || polyline.Length < 2)
        {
            return p;
        }

        float bestDist = float.MaxValue;
        Vector3 bestPoint = polyline[0];
        float bestCumulative = 0f;
        float running = 0f;

        for (int i = 1; i < polyline.Length; i++)
        {
            Vector3 a = polyline[i - 1];
            Vector3 b = polyline[i];
            float segLen = Vector3.Distance(a, b);

            Vector3 ab = b - a;
            Vector3 ap = p - a;
            float t = segLen > 0.001f ? Mathf.Clamp01(Vector3.Dot(ap, ab) / (segLen * segLen)) : 0f;
            Vector3 proj = a + t * ab;
            float dist = Vector3.Distance(p, proj);

            if (dist < bestDist)
            {
                bestDist = dist;
                bestPoint = proj;
                bestCumulative = running + t * segLen;
            }

            running += segLen;
        }

        cumulativeDistance = bestCumulative;
        return bestPoint;
    }

    public static Vector3 SamplePolylineAtDistance(Vector3[] corners, float targetDistance)
    {
        if (corners == null || corners.Length < 2) return corners?[corners.Length - 1] ?? Vector3.zero;

        float running = 0f;
        for (int i = 1; i < corners.Length; i++)
        {
            float segLen = Vector3.Distance(corners[i - 1], corners[i]);
            if (running + segLen >= targetDistance || i == corners.Length - 1)
            {
                float t = segLen > 0.001f ? (targetDistance - running) / segLen : 0f;
                t = Mathf.Clamp01(t);
                return Vector3.Lerp(corners[i - 1], corners[i], t);
            }
            running += segLen;
        }
        return corners[corners.Length - 1];
    }

    public static float TotalLength(Vector3[] corners)
    {
        if (corners == null || corners.Length < 2) return 0f;
        float total = 0f;
        for (int i = 1; i < corners.Length; i++)
            total += Vector3.Distance(corners[i - 1], corners[i]);
        return total;
    }
}
