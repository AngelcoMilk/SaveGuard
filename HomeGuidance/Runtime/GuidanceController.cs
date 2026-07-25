using System;
using System.Collections.Generic;
using UnityEngine;
using HomeGuidance.Arrival;
using HomeGuidance.Routing;
using HomeGuidance.Compass;
using HomeGuidance.Trail;

namespace HomeGuidance.Runtime;

public sealed class GuidanceController
{
    // ── Owned state ──
    public readonly RoundGuidanceState RoundGuidanceState = new();
    public readonly GuidanceLifecycle Lifecycle;

    private readonly ArrivalTracker _arrivalTracker = new();
    private TeleporterGraphProvider _teleporterProvider;
    private RoutePlanner _routePlanner;
    private CompassVisualAdapter _compassAdapter;
    private GuidanceTrailPresenter _trailPresenter;

    private GuidanceDirtyFlags _dirtyFlags;
    private GuidanceSnapshot _lastSnapshot;
    private RoutePlan _activePlan;

    // ── Timing ──
    private float _nextArrivalScanTime;
    private float _nextRouteCheckTime;
    private float _nextRetryTime;
    private float _nextTeleporterEnumTime;
    private int _currentRoundToken;

    // ── Teleport dedup ──
    private int _lastTeleportObservedFrame;
    private int _lastTeleportPawnInstanceId;
    private Vector3 _lastTeleportActualPosition;
    private bool _previousPositionValid;
    private int _suppressJumpDetectionUntilFrame;
    private int _teleportGeneration;

    // ── Compass attach state ──
    private UICompassRef _attachedCompass;
    private UICompassRef _pendingCompass;
    private int _pendingVisualRefsFrameCount;
    private const int MaxPendingFrames = 60;
    private float _nextCompassFindTime;
    private const float CompassFindInterval = 1.0f;

    public GuidanceController()
    {
        Lifecycle = new GuidanceLifecycle(this);
    }

    public void Initialize()
    {
        _teleporterProvider = new TeleporterGraphProvider(this);
        _routePlanner = new RoutePlanner(this);
        _compassAdapter = new CompassVisualAdapter(this);
        _trailPresenter = new GuidanceTrailPresenter(this);

        Lifecycle.Initialize();

        // Immediate find of existing UICompass (including inactive)
        FindAndAttachCompass();

        GuidanceLog.Info("GuidanceController initialized");
    }

    // ── Round callbacks ──

    public void OnRoundBegin(int token)
    {
        _currentRoundToken = token;
        _dirtyFlags |= GuidanceDirtyFlags.RoundChanged | GuidanceDirtyFlags.RouteRequested;
        _nextArrivalScanTime = 0f;
        _nextRouteCheckTime = 0f;
        _nextTeleporterEnumTime = 0f;
        _previousPositionValid = false;
        _teleportGeneration = 0;
        _activePlan = null;
        _routePlanner.Clear();
    }

    public void OnRoundEnd()
    {
        _activePlan = null;
        _lastSnapshot = null;
        _trailPresenter?.Hide();
        _compassAdapter?.HideArrow();
        _routePlanner.Clear();
        _previousPositionValid = false;
    }

    // ── Tick ──

    public void Tick(float now, float deltaTime)
    {
        if (!Plugin.ModConfig.Enabled.Value || !RoundGuidanceState.RoundActive)
        {
            Lifecycle.Tick();
            return;
        }

        Lifecycle.Tick();

        // Low-frequency compass find (only when not attached/pending)
        if (_attachedCompass == null && _pendingCompass == null && now >= _nextCompassFindTime)
        {
            FindAndAttachCompass();
            _nextCompassFindTime = now + CompassFindInterval;
        }

        // Pending visual refs retry
        if (_pendingCompass != null)
        {
            RetryPendingVisualRefs(now);
        }

        // Arrival scan
        if (now >= _nextArrivalScanTime)
        {
            RunArrivalScan(now);
            _nextArrivalScanTime = now + Plugin.ModConfig.ArrivalScanInterval.Value;
        }

        // Position jump fallback (every frame)
        CheckPositionJump(now);

        // Snapshot comparison + teleporter enumeration (at route check cadence)
        if (now >= _nextRouteCheckTime)
        {
            CompareSnapshotAndGenerateDirty();
            RunRouteCheck(now);
            _nextRouteCheckTime = now + Plugin.ModConfig.RouteCheckInterval.Value;
        }

        // Periodic teleporter graph refresh (at a slower cadence)
        if (now >= _nextTeleporterEnumTime)
        {
            _teleporterProvider.Refresh();
            _nextTeleporterEnumTime = now + 1.0f; // refresh teleporter state every second
        }

        // Handle dirty flags and replan
        if (_dirtyFlags != GuidanceDirtyFlags.None && now >= _nextRetryTime)
        {
            ProcessDirtyFlags(now);
        }
    }

    public void LateTick(float now, float deltaTime)
    {
        if (!Plugin.ModConfig.Enabled.Value || !RoundGuidanceState.RoundActive)
            return;

        if (_activePlan == null || !_activePlan.IsValid) return;

        // Update arrow
        if (ShouldShowArrow())
            _compassAdapter?.UpdateArrow(_activePlan);
        else
            _compassAdapter?.HideArrow();

        // Update trail animation
        if (ShouldShowTrail())
            _trailPresenter?.UpdateAnimation(now);
        else
            _trailPresenter?.Hide();
    }

    // ── Dirty flags ──

    public void MarkDirty(GuidanceDirtyFlags flags)
    {
        _dirtyFlags |= flags;
    }

    private void ProcessDirtyFlags(float now)
    {
        // Priority order
        if ((_dirtyFlags & GuidanceDirtyFlags.RoundChanged) != 0)
        {
            _dirtyFlags &= ~GuidanceDirtyFlags.RoundChanged;
        }
        if ((_dirtyFlags & GuidanceDirtyFlags.PlayerTeleported) != 0)
        {
            _trailPresenter?.Hide();
            _activePlan = null;
            _routePlanner.ClearStartEdges();
            _dirtyFlags = (_dirtyFlags & ~GuidanceDirtyFlags.PlayerTeleported) | GuidanceDirtyFlags.RouteRequested;
        }

        if ((_dirtyFlags & (GuidanceDirtyFlags.RouteRequested | GuidanceDirtyFlags.RouteDeviation |
            GuidanceDirtyFlags.RouteInvalid | GuidanceDirtyFlags.GraphTopologyChanged |
            GuidanceDirtyFlags.TeleportCostChanged | GuidanceDirtyFlags.ExtractionChanged |
            GuidanceDirtyFlags.LocalPawnChanged | GuidanceDirtyFlags.ConfigChanged)) != 0)
        {
            ComputeRoute(now);
            _dirtyFlags &= ~(GuidanceDirtyFlags.RouteRequested | GuidanceDirtyFlags.RouteDeviation |
                GuidanceDirtyFlags.RouteInvalid | GuidanceDirtyFlags.GraphTopologyChanged |
                GuidanceDirtyFlags.TeleportCostChanged | GuidanceDirtyFlags.ExtractionChanged |
                GuidanceDirtyFlags.LocalPawnChanged | GuidanceDirtyFlags.ConfigChanged);
        }
    }

    // ── Arrival ──

    private void RunArrivalScan(float now)
    {
        var gm = YAPYAP.GameManager.Instance;
        if (gm == null || !gm.RoundActive) return;

        var extraction = GetCurrentExtraction();
        if (extraction == null) return;

        var scan = _arrivalTracker.Scan(now, RoundGuidanceState.CurrentRoundToken,
            RoundGuidanceState.ReachedPlayerNetIds, extraction, Plugin.ModConfig.ArrivalRadius.Value);

        if (!scan.AnyCandidate) return;

        var newlyReached = new List<uint>();
        foreach (var netId in scan.CandidateNetIds)
        {
            if (RoundGuidanceState.MarkReached(netId, scan.RoundToken))
                newlyReached.Add(netId);
        }

        if (newlyReached.Count > 0)
        {
            foreach (var id in newlyReached)
                GuidanceLog.Info($"Player arrived: netId={id}");

            MarkDirty(GuidanceDirtyFlags.ArrivalStateChanged);

            var local = YAPYAP.Pawn.LocalInstance;
            if (local != null && newlyReached.Contains(local.netId))
            {
                _trailPresenter?.Hide();
            }
        }
    }

    private void RunRouteCheck(float now)
    {
        if (_activePlan == null || !_activePlan.IsValid) return;

        var local = YAPYAP.Pawn.LocalInstance;
        if (local == null) return;

        // Deviation check
        float dist = PathGeometry.MinDistanceToPolyline(local.transform.position, _activePlan.CurrentWalkCorners);
        if (dist > Plugin.ModConfig.RouteDeviationDistance.Value)
        {
            MarkDirty(GuidanceDirtyFlags.RouteDeviation | GuidanceDirtyFlags.RouteRequested);
        }
    }

    private void CompareSnapshotAndGenerateDirty()
    {
        var snap = BuildCurrentSnapshot();
        if (_lastSnapshot == null)
        {
            _lastSnapshot = snap;
            return;
        }

        if (snap.RoundActive != _lastSnapshot.RoundActive)
            MarkDirty(GuidanceDirtyFlags.RoundChanged);

        if (snap.LocalPawnInstanceId != _lastSnapshot.LocalPawnInstanceId)
            MarkDirty(GuidanceDirtyFlags.LocalPawnChanged);

        if (snap.ExtractionInstanceId != _lastSnapshot.ExtractionInstanceId)
            MarkDirty(GuidanceDirtyFlags.ExtractionChanged);

        if (snap.TeleporterTopologyHash != _lastSnapshot.TeleporterTopologyHash)
            MarkDirty(GuidanceDirtyFlags.GraphTopologyChanged);

        if (snap.TeleporterCostHash != _lastSnapshot.TeleporterCostHash)
            MarkDirty(GuidanceDirtyFlags.TeleportCostChanged);

        if (snap.CompassChanged(_lastSnapshot))
            MarkDirty(GuidanceDirtyFlags.CompassChanged);

        _lastSnapshot = snap;
    }

    private GuidanceSnapshot BuildCurrentSnapshot()
    {
        var local = YAPYAP.Pawn.LocalInstance;
        var extraction = GetCurrentExtraction();
        var (topoHash, costHash) = _teleporterProvider.GetHashes();

        var compass = _attachedCompass?.Resolve();
        bool compassEnabled = false;
        if (compass != null)
        {
            var enabledField = typeof(YAPYAP.UICompass).GetField("_enabled",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            compassEnabled = enabledField != null && (bool)enabledField.GetValue(compass);
        }

        return new GuidanceSnapshot
        {
            RoundActive = RoundGuidanceState.RoundActive,
            LocalPawnInstanceId = local != null ? local.GetInstanceID() : 0,
            LocalPawnNetId = local != null ? local.netId : 0,
            LocalPawnAlive = local != null && !local.IsDead,
            LocalPawnExtracted = local != null && local.IsExtracted,
            ExtractionInstanceId = extraction != null ? extraction.GetInstanceID() : 0,
            ExtractionPosition = extraction != null ? extraction.transform.position : Vector3.zero,
            CompassInstanceId = compass != null ? compass.GetInstanceID() : 0,
            CompassSettingEnabled = compassEnabled,
            TeleporterTopologyHash = topoHash,
            TeleporterCostHash = costHash
        };
    }

    private void ComputeRoute(float now)
    {
        var plan = _routePlanner.ComputePlan(BuildRouteRequest(now), _activePlan, _teleportGeneration, _currentRoundToken);
        if (plan == null || !plan.IsValid)
        {
            if (_activePlan != null && _activePlan.IsValid)
            {
                // Keep current plan briefly, will retry
                _nextRetryTime = now + Plugin.ModConfig.RouteRetryInterval.Value;
            }
            else
            {
                _activePlan = null;
                _trailPresenter?.Hide();
            }
            return;
        }

        // Apply selection policy
        if (!RouteSelectionPolicy.ShouldReplace(_activePlan, plan, Plugin.ModConfig.RouteSwitchGainSeconds.Value))
            return;

        _activePlan = plan;
        _trailPresenter?.Submit(plan);

        if (Plugin.ModConfig.DebugLogging.Value)
        {
            GuidanceLog.Debug($"Route planned reason={plan.Reason} cost={plan.TotalCostSeconds:F2}s edges={plan.Edges.Count}");
        }
    }

    private RouteRequest BuildRouteRequest(float now)
    {
        var local = YAPYAP.Pawn.LocalInstance;
        var extraction = GetCurrentExtraction();
        Vector3 playerPos = local != null ? local.transform.position : Vector3.zero;

        var request = new RouteRequest
        {
            RoundToken = _currentRoundToken,
            ActualPlayerPosition = playerPos,
            SampledStart = playerPos,
            ExtractionVisualPosition = extraction != null ? extraction.transform.position : Vector3.zero,
            ExtractionInstanceId = extraction != null ? extraction.GetInstanceID() : 0,
            Teleporters = _teleporterProvider?.GetSnapshots() ?? System.Array.Empty<TeleporterSnapshot>(),
            EstimatedWalkSpeed = Plugin.ModConfig.EstimatedWalkSpeed.Value,
            TeleportCountdownSeconds = Plugin.ModConfig.TeleportCountdownSeconds.Value,
            TeleportWaitSeconds = Plugin.ModConfig.TeleportWaitSeconds.Value,
            Now = now,
            Reason = _activePlan == null ? RouteReplanReason.Initial : RouteReplanReason.Periodic
        };

        return request;
    }

    // ── Compass attach ──

    public void AttachCompass(YAPYAP.UICompass compass)
    {
        if (compass == null) return;
        int id = compass.GetInstanceID();

        if (_attachedCompass?.InstanceId == id) return;

        if (_pendingCompass != null && _pendingCompass.InstanceId != id)
        {
            // Replace pending with new candidate
            GuidanceLog.Debug($"Replacing pending compass {_pendingCompass.InstanceId} with {id}");
            _pendingCompass = null;
            _pendingVisualRefsFrameCount = 0;
        }

        _pendingCompass = new UICompassRef(compass);
        _pendingVisualRefsFrameCount = 0;
    }

    private void FindAndAttachCompass()
    {
        var compasses = YAPYAP.UICompass.FindObjectsByType<YAPYAP.UICompass>(
            UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);

        if (compasses == null || compasses.Length == 0) return;

        YAPYAP.UICompass best = null;
        foreach (var c in compasses)
        {
            if (c == null) continue;
            if (best == null ||
                (c.isActiveAndEnabled && !best.isActiveAndEnabled))
            {
                best = c;
            }
            else if (c.isActiveAndEnabled == best.isActiveAndEnabled)
            {
                // Both same priority; prefer the one already pending/attached
                int bestId = best.GetInstanceID();
                int cId = c.GetInstanceID();
                if ((_attachedCompass?.InstanceId == cId || _pendingCompass?.InstanceId == cId) &&
                    _attachedCompass?.InstanceId != bestId && _pendingCompass?.InstanceId != bestId)
                {
                    best = c;
                }
            }
        }

        if (best != null && (_attachedCompass == null || _attachedCompass.InstanceId != best.GetInstanceID()))
        {
            if (_pendingCompass == null || _pendingCompass.InstanceId != best.GetInstanceID())
            {
                AttachCompass(best);
            }
        }
        else if (compasses.Length > 1 && best == null)
        {
            GuidanceLog.Warning("Multiple UICompass candidates with equal priority; not modifying any Renderer.");
        }
    }

    private void RetryPendingVisualRefs(float now)
    {
        if (_pendingCompass == null) return;

        var compass = _pendingCompass.Resolve();
        if (compass == null)
        {
            // Fake-null — detach and resume search
            _pendingCompass = null;
            _pendingVisualRefsFrameCount = 0;
            _nextCompassFindTime = now + CompassFindInterval;
            return;
        }

        var visualRefsField = typeof(YAPYAP.UICompass).GetField("_visualRefs",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var visualRefs = visualRefsField?.GetValue(compass) as YAPYAP.UICompassReferences;

        if (visualRefs != null && visualRefs.RenderTargetRoot != null)
        {
            _compassAdapter.DoAttach(compass, visualRefs);
            _attachedCompass = _pendingCompass;
            _pendingCompass = null;
            _pendingVisualRefsFrameCount = 0;
            GuidanceLog.Info($"Compass attached: instanceId={_attachedCompass.InstanceId}");
        }
        else
        {
            _pendingVisualRefsFrameCount++;
            if (_pendingVisualRefsFrameCount >= MaxPendingFrames)
            {
                GuidanceLog.Warning($"Compass _visualRefs not ready after {MaxPendingFrames} frames; retryable.");
                _pendingCompass = null;
                _pendingVisualRefsFrameCount = 0;
                _nextCompassFindTime = now + CompassFindInterval;
            }
        }
    }

    public void DetachCompass(YAPYAP.UICompass compass)
    {
        if (compass == null) return;
        int id = compass.GetInstanceID();

        if (_attachedCompass?.InstanceId == id)
        {
            _compassAdapter?.DoDetach();
            _attachedCompass = null;
        }
        if (_pendingCompass?.InstanceId == id)
        {
            _pendingCompass = null;
            _pendingVisualRefsFrameCount = 0;
        }
    }

    public void NotifyCompassEnabled(YAPYAP.UICompass compass, bool enabled)
    {
        // The SetEnabled Postfix — just sync the setting gate.
        // Arrow visibility is determined in ShouldShowArrow() using currentTarget/activeFrame.
        if (!enabled)
            _compassAdapter?.HideArrow();
        // enabled=true does NOT unconditionally show — gated by full conditions in LateTick
    }

    // ── Teleport notification ──

    public void NotifyLocalPawnTeleported(YAPYAP.Pawn pawn)
    {
        if (pawn == null || !pawn.isLocalPlayer) return;
        if (!RoundGuidanceState.RoundActive) return;
        if (pawn.IsDead || pawn.IsExtracted) return;

        var actual = pawn.transform.position;
        int frame = Time.frameCount;
        int id = pawn.GetInstanceID();

        // Dedup: same pawn + same frame, or adjacent frame + close position
        bool duplicate = id == _lastTeleportPawnInstanceId &&
            (frame == _lastTeleportObservedFrame ||
             (frame <= _lastTeleportObservedFrame + 1 && Vector3.Distance(actual, _lastTeleportActualPosition) < 0.25f));

        if (duplicate) return;

        _teleportGeneration++;
        _lastTeleportObservedFrame = frame;
        _lastTeleportPawnInstanceId = id;
        _lastTeleportActualPosition = actual;
        _previousPositionValid = true;
        _suppressJumpDetectionUntilFrame = frame + 1;

        _trailPresenter?.Hide();
        _activePlan = null;
        _routePlanner.ClearStartEdges();
        MarkDirty(GuidanceDirtyFlags.PlayerTeleported);

        GuidanceLog.Debug($"Teleport observed: gen={_teleportGeneration} frame={frame}");
    }

    private void CheckPositionJump(float now)
    {
        var local = YAPYAP.Pawn.LocalInstance;
        if (local == null || !RoundGuidanceState.RoundActive || local.IsDead || local.IsExtracted) return;

        var actual = local.transform.position;
        int frame = Time.frameCount;

        if (!_previousPositionValid || frame <= _suppressJumpDetectionUntilFrame)
        {
            _previousPositionValid = true;
            _lastTeleportActualPosition = actual;
            return;
        }

        float dist = Vector3.Distance(_lastTeleportActualPosition, actual);
        _lastTeleportActualPosition = actual;

        if (dist > Plugin.ModConfig.PositionJumpThreshold.Value)
        {
            GuidanceLog.Debug($"Position jump detected: {dist:F1}m");
            NotifyLocalPawnTeleported(local);
        }
    }

    // ── Visual gating ──

    private bool ShouldShowArrow()
    {
        if (!Plugin.ModConfig.Enabled.Value) return false;
        if (_attachedCompass == null) return false;

        var compass = _attachedCompass.Resolve();
        if (compass == null) return false;

        // Check Compass setting gate
        var enabledField = typeof(YAPYAP.UICompass).GetField("_enabled",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        bool compassEnabled = enabledField != null && (bool)enabledField.GetValue(compass);

        var currentTargetField = typeof(YAPYAP.UICompass).GetField("currentTarget",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var currentTarget = currentTargetField?.GetValue(compass) as Transform;

        var activeFrameField = typeof(YAPYAP.UICompass).GetField("compassActiveFrame",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var activeFrame = activeFrameField?.GetValue(compass) as GameObject;

        if (!compassEnabled || currentTarget == null || activeFrame == null || !activeFrame.activeSelf)
            return false;

        if (!RoundGuidanceState.RoundActive) return false;

        var local = YAPYAP.Pawn.LocalInstance;
        if (local == null || local.IsDead || local.IsExtracted) return false;

        if (_activePlan == null || !_activePlan.IsValid) return false;
        if (_activePlan.CurrentWalkCorners == null || _activePlan.CurrentWalkCorners.Length == 0)
        {
            // MarkerOnly teleport entrance is still valid for arrow direction
            if (!_activePlan.CurrentSubTargetIsTeleportEntrance) return false;
        }

        return true;
    }

    private bool ShouldShowTrail()
    {
        if (!Plugin.ModConfig.Enabled.Value) return false;
        if (!RoundGuidanceState.RoundActive) return false;
        if (!RoundGuidanceState.GuidanceUnlocked) return false;

        var local = YAPYAP.Pawn.LocalInstance;
        if (local == null || local.IsDead || local.IsExtracted) return false;
        if (RoundGuidanceState.HasReached(local.netId)) return false;

        if (_activePlan == null || !_activePlan.IsValid) return false;
        if (_activePlan.DisplaySegment == null) return false;

        if (_activePlan.DisplaySegment.Corners == null || _activePlan.DisplaySegment.Corners.Length == 0)
        {
            // MarkerOnly teleport entrance
            return _activePlan.DisplaySegment.EndsAtTeleport;
        }

        return true;
    }

    private YAPYAP.TeleportExtractionCircle GetCurrentExtraction()
    {
        return YAPYAP.TeleportExtractionCircle.FindFirstObjectByType<YAPYAP.TeleportExtractionCircle>(
            UnityEngine.FindObjectsInactive.Exclude);
    }

    // ── Shutdown ──

    public void Shutdown()
    {
        Lifecycle.Shutdown();
        _compassAdapter?.DoDetach();
        _trailPresenter?.DestroyPool();
        _activePlan = null;
        _lastSnapshot = null;
        _routePlanner?.Clear();
        GuidanceLog.Info("GuidanceController shutdown");
    }
}

// ── Internal helper ──

internal sealed class UICompassRef
{
    private readonly WeakReference<YAPYAP.UICompass> _ref;
    public int InstanceId { get; }

    public UICompassRef(YAPYAP.UICompass compass)
    {
        _ref = new WeakReference<YAPYAP.UICompass>(compass);
        InstanceId = compass.GetInstanceID();
    }

    public YAPYAP.UICompass Resolve()
    {
        return _ref.TryGetTarget(out var c) ? c : null;
    }
}
