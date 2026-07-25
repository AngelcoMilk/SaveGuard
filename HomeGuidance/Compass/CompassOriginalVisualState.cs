using System.Collections.Generic;
using UnityEngine;

namespace HomeGuidance.Compass;

public sealed class CompassOriginalVisualState
{
    private readonly List<RendererRecord> _records = new();

    private struct RendererRecord
    {
        public Renderer Renderer;
        public bool OriginalEnabled;
        public int InstanceId;
    }

    public void Record(Renderer renderer)
    {
        if (renderer == null) return;
        _records.Add(new RendererRecord
        {
            Renderer = renderer,
            OriginalEnabled = renderer.enabled,
            InstanceId = renderer.GetInstanceID()
        });
    }

    public void DisableAll()
    {
        foreach (var r in _records)
        {
            if (r.Renderer != null)
                r.Renderer.enabled = false;
        }
    }

    public void RestoreAll()
    {
        foreach (var r in _records)
        {
            if (r.Renderer != null)
                r.Renderer.enabled = r.OriginalEnabled;
        }
        _records.Clear();
    }

    public bool HasRecord(int instanceId)
    {
        foreach (var r in _records)
            if (r.InstanceId == instanceId) return true;
        return false;
    }
}
