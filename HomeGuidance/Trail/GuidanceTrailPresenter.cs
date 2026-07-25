using System.Collections.Generic;
using UnityEngine;
using HomeGuidance.Runtime;
using HomeGuidance.Routing;

namespace HomeGuidance.Trail;

public sealed class GuidanceTrailPresenter
{
    private readonly GuidanceController _controller;
    private readonly TrailDotPool _pool;
    private TeleportEntranceMarker _entranceMarker;

    public GuidanceTrailPresenter(GuidanceController controller)
    {
        _controller = controller;
        _pool = new TrailDotPool();
    }

    public void Submit(RoutePlan plan)
    {
        Hide();

        if (plan == null || !plan.IsValid || plan.DisplaySegment == null)
            return;

        var segment = plan.DisplaySegment;

        // MarkerOnly case
        if ((segment.Corners == null || segment.Corners.Length < 2) && segment.EndsAtTeleport)
        {
            if (_entranceMarker == null)
                _entranceMarker = TeleportEntranceMarker.Create();
            _entranceMarker.ShowAt(segment.SubTarget);
            return;
        }

        if (segment.Corners == null || segment.Corners.Length < 2)
            return;

        // Sample dots along the cleaned corners
        var points = TrailSampling.SampleArcLength(segment.Corners,
            Plugin.ModConfig.TrailDotSpacing.Value, Plugin.ModConfig.TrailMaxDots.Value);

        // Apply ground offset
        for (int i = 0; i < points.Length; i++)
            points[i].y += Plugin.ModConfig.TrailGroundOffset.Value;

        _pool.ShowAt(points);

        // Show entrance marker if segment ends at teleport
        if (segment.EndsAtTeleport)
        {
            if (_entranceMarker == null)
                _entranceMarker = TeleportEntranceMarker.Create();
            _entranceMarker.ShowAt(segment.SubTarget + Vector3.up * Plugin.ModConfig.TrailGroundOffset.Value);
        }
    }

    public void UpdateAnimation(float now)
    {
        _pool.UpdateAnimation(now);
    }

    public void Hide()
    {
        _pool.HideAll();
        _entranceMarker?.Hide();
    }

    public void DestroyPool()
    {
        _pool.Destroy();
        _entranceMarker?.Destroy();
        _entranceMarker = null;
    }
}
