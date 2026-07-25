using System.Collections.Generic;
using UnityEngine;

namespace HomeGuidance.Trail;

public sealed class TrailDotPool
{
    private readonly List<TrailDotView> _pool = new();
    private readonly Queue<int> _activeIndices = new();

    public void ShowAt(Vector3[] positions)
    {
        HideAll();

        int needed = positions.Length;
        int max = Plugin.ModConfig.TrailMaxDots.Value;
        if (needed > max) needed = max;

        // Grow pool if needed
        while (_pool.Count < needed)
        {
            _pool.Add(TrailDotView.Create());
        }

        for (int i = 0; i < needed; i++)
        {
            var dot = _pool[i];
            dot.SetPosition(positions[i]);
            dot.SetVisible(true);
            dot.SetPhase((float)i / Mathf.Max(1, needed));
            _activeIndices.Enqueue(i);
        }
    }

    public void UpdateAnimation(float now)
    {
        float speed = 0.5f;
        float frequency = 2f;

        foreach (var i in _activeIndices)
        {
            if (i < _pool.Count)
                _pool[i].Animate(now, speed, frequency);
        }
    }

    public void HideAll()
    {
        foreach (var i in _activeIndices)
        {
            if (i < _pool.Count)
                _pool[i].SetVisible(false);
        }
        _activeIndices.Clear();
    }

    public void Destroy()
    {
        foreach (var dot in _pool)
            dot.Destroy();
        _pool.Clear();
        _activeIndices.Clear();
    }
}
