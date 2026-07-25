using System.Collections.Generic;
using UnityEngine;
using HomeGuidance.Runtime;
using HomeGuidance.Routing;

namespace HomeGuidance.Compass;

public sealed class CompassVisualAdapter
{
    private readonly GuidanceController _controller;
    private CompassOriginalVisualState _originalState;
    private NavigationArrowLayer _arrowLayer;
    private CompassHierarchyProfile _profile;
    private ArrowColorState _colorState;

    public CompassVisualAdapter(GuidanceController controller)
    {
        _controller = controller;
        _profile = CompassHierarchyProfile.CreatePlaceholder();
        _colorState = new ArrowColorState();
    }

    public void DoAttach(YAPYAP.UICompass compass, YAPYAP.UICompassReferences visualRefs)
    {
        if (Plugin.ModConfig.DebugLogging.Value)
            CompassHierarchyProbe.DumpHierarchy(visualRefs);

        var renderTargetRoot = visualRefs.RenderTargetRoot;
        if (renderTargetRoot == null)
        {
            GuidanceLog.Error("Compass attach failed: RenderTargetRoot is null");
            return;
        }

        var renderTargetTransform = renderTargetRoot.transform;

        // Validate profile
        if (!_profile.ValidatesAgainst(renderTargetTransform))
        {
            GuidanceLog.Error("Compass attach failed: hierarchy profile does not match. " +
                "Run CompassHierarchyProbe and freeze the profile before publishing.");
            return;
        }

        // Store original state and disable direction renderers
        _originalState = new CompassOriginalVisualState();

        foreach (var path in _profile.DirectionRendererAllowList)
        {
            var t = renderTargetTransform.Find(path);
            if (t == null) continue;
            var r = t.GetComponent<Renderer>();
            if (r != null)
                _originalState.Record(r);
        }

        _originalState.DisableAll();

        // Create arrow layer
        Transform arrowParent = renderTargetTransform;
        if (!string.IsNullOrEmpty(_profile.ArrowParentRelativePath))
        {
            var parent = renderTargetTransform.Find(_profile.ArrowParentRelativePath);
            if (parent != null) arrowParent = parent;
        }

        _arrowLayer = NavigationArrowLayer.Create(arrowParent, _profile);
    }

    public void DoDetach()
    {
        _originalState?.RestoreAll();
        _originalState = null;

        if (_arrowLayer != null)
        {
            _arrowLayer.Destroy();
            _arrowLayer = null;
        }
    }

    public void UpdateArrow(RoutePlan plan)
    {
        if (_arrowLayer == null) return;
        if (plan == null || !plan.IsValid) return;

        var local = YAPYAP.Pawn.LocalInstance;
        if (local == null) return;

        var camera = GetMainCamera();
        if (camera == null) return;

        // Determine look-ahead point
        Vector3 lookAheadPoint;
        if (plan.CurrentWalkCorners != null && plan.CurrentWalkCorners.Length >= 2)
        {
            lookAheadPoint = ResolveLookAhead(local.transform.position, plan.CurrentWalkCorners);
        }
        else if (plan.CurrentSubTargetIsTeleportEntrance)
        {
            lookAheadPoint = plan.CurrentSubTarget;
        }
        else
        {
            _arrowLayer.SetHidden();
            return;
        }

        // Signed angle
        float? angle = ArrowDirectionSolver.ComputeSignedAngle(camera.transform, local.transform.position, lookAheadPoint);
        if (angle == null)
        {
            _arrowLayer.SetHidden();
            return;
        }

        // Color
        float deltaY = lookAheadPoint.y - local.transform.position.y;
        var color = _colorState.Update(plan.CurrentSubTargetIsTeleportEntrance, deltaY);

        _arrowLayer.SetAngle(angle.Value);
        _arrowLayer.SetColor(color);
        _arrowLayer.SetVisible(true);
    }

    public void HideArrow()
    {
        _arrowLayer?.SetHidden();
    }

    private static Vector3 ResolveLookAhead(Vector3 playerPos, Vector3[] corners)
    {
        if (corners == null || corners.Length < 2) return corners?[corners.Length - 1] ?? playerPos;

        // Find projection on polyline
        PathGeometry.FindClosestProjection(playerPos, corners, out float startDist);

        float lookAhead = Plugin.ModConfig.LookAheadDistance.Value;
        float skipNear = Plugin.ModConfig.SkipNearCornerDistance.Value;
        float desired = startDist + lookAhead;
        float total = PathGeometry.TotalLength(corners);

        var point = PathGeometry.SamplePolylineAtDistance(corners, Mathf.Min(desired, total));

        // Skip corners too close to player
        float extra = 0f;
        while (Vector3.Distance(playerPos, point) < skipNear && desired + extra < total)
        {
            extra += 0.5f;
            point = PathGeometry.SamplePolylineAtDistance(corners, Mathf.Min(desired + extra, total));
        }

        return point;
    }

    private static Camera GetMainCamera()
    {
        var cm = YAPYAP.CameraManager.Instance;
        if (cm != null && cm.MainCamera != null) return cm.MainCamera;

        var cam = Camera.main;
        return cam;
    }
}
