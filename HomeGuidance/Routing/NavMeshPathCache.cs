using System.Collections.Generic;
using UnityEngine;

namespace HomeGuidance.Routing;

public sealed class NavMeshPathCache
{
    private readonly Dictionary<long, CachedWalkPath> _cache = new();
    private readonly Dictionary<long, float> _negativeCache = new();
    private int _roundToken;

    private struct CachedWalkPath
    {
        public Vector3[] Corners;
        public float Length;
    }

    public void Clear()
    {
        _cache.Clear();
        _negativeCache.Clear();
    }

    public void OnNewRound(int roundToken)
    {
        _roundToken = roundToken;
        Clear();
    }

    public bool TryGetFixed(Vector3 from, Vector3 to, int fromId, int toId, out Vector3[] corners, out float length)
    {
        long key = MakeKey(from, to, fromId, toId);
        if (_cache.TryGetValue(key, out var cached))
        {
            corners = cached.Corners;
            length = cached.Length;
            return true;
        }
        corners = null;
        length = 0f;
        return false;
    }

    public void StoreFixed(Vector3 from, Vector3 to, int fromId, int toId, Vector3[] corners, float length)
    {
        long key = MakeKey(from, to, fromId, toId);
        _cache[key] = new CachedWalkPath { Corners = corners, Length = length };
    }

    public bool IsNegativelyCached(int fromId, int toId, float now, float negativeDuration)
    {
        long key = ((long)fromId << 32) | (uint)toId;
        if (_negativeCache.TryGetValue(key, out var expiry) && expiry > now)
            return true;
        return false;
    }

    public void AddNegative(int fromId, int toId, float now, float negativeDuration)
    {
        long key = ((long)fromId << 32) | (uint)toId;
        _negativeCache[key] = now + negativeDuration;
    }

    private static long MakeKey(Vector3 a, Vector3 b, int idA, int idB)
    {
        uint qax = (uint)(a.x * 4f);
        uint qaz = (uint)(a.z * 4f);
        uint qbx = (uint)(b.x * 4f);
        uint qbz = (uint)(b.z * 4f);
        long combined = ((long)idA << 48) | ((long)idB << 32);
        combined ^= ((long)qax << 16) | ((long)qaz);
        combined ^= ((long)qbx) | ((long)qbz << 16);
        return combined;
    }
}
