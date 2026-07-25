using System;
using UnityEngine;

namespace HomeGuidance.Trail;

public static class TrailSampling
{
    /// <summary>
    /// Sample points uniformly along arc length of a polyline.
    /// </summary>
    public static Vector3[] SampleArcLength(Vector3[] corners, float spacing, int maxDots)
    {
        if (corners == null || corners.Length < 2)
            return Array.Empty<Vector3>();

        // Build cumulative lengths
        var cumulative = new float[corners.Length];
        cumulative[0] = 0f;
        for (int i = 1; i < corners.Length; i++)
            cumulative[i] = cumulative[i - 1] + Vector3.Distance(corners[i - 1], corners[i]);

        float total = cumulative[corners.Length - 1];
        if (total < 0.01f)
            return corners.Length >= 2 ? new[] { corners[0], corners[corners.Length - 1] } : new[] { corners[0] };

        int count = Mathf.Min(maxDots, Mathf.FloorToInt(total / spacing) + 1);
        if (count < 2) count = 2;

        var points = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            float d = Mathf.Clamp((float)i / (count - 1) * total, 0f, total);
            points[i] = SampleAtDistance(corners, cumulative, d);
        }

        return points;
    }

    private static Vector3 SampleAtDistance(Vector3[] corners, float[] cumulative, float distance)
    {
        // Binary search for segment
        int lo = 0, hi = cumulative.Length - 1;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (cumulative[mid] < distance)
                lo = mid + 1;
            else
                hi = mid;
        }

        if (lo == 0) return corners[0];

        int segEnd = lo;
        int segStart = lo - 1;
        float segLen = cumulative[segEnd] - cumulative[segStart];
        float t = segLen > 0.001f ? (distance - cumulative[segStart]) / segLen : 0f;
        t = Mathf.Clamp01(t);

        return Vector3.Lerp(corners[segStart], corners[segEnd], t);
    }
}
