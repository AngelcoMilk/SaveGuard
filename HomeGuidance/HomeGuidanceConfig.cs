using BepInEx.Configuration;

namespace HomeGuidance;

public sealed class HomeGuidanceConfig
{
    public ConfigEntry<bool> Enabled { get; }
    public ConfigEntry<float> ArrivalRadius { get; }
    public ConfigEntry<float> ArrivalScanInterval { get; }
    public ConfigEntry<float> RouteCheckInterval { get; }
    public ConfigEntry<float> RouteRetryInterval { get; }
    public ConfigEntry<float> RouteDeviationDistance { get; }
    public ConfigEntry<float> EstimatedWalkSpeed { get; }
    public ConfigEntry<float> TeleportCountdownSeconds { get; }
    public ConfigEntry<float> TeleportWaitSeconds { get; }
    public ConfigEntry<float> RouteSwitchGainSeconds { get; }
    public ConfigEntry<float> LookAheadDistance { get; }
    public ConfigEntry<float> SkipNearCornerDistance { get; }
    public ConfigEntry<float> VerticalEnterThreshold { get; }
    public ConfigEntry<float> VerticalExitThreshold { get; }
    public ConfigEntry<float> ArrowSmoothTime { get; }
    public ConfigEntry<float> TrailDotSpacing { get; }
    public ConfigEntry<int> TrailMaxDots { get; }
    public ConfigEntry<float> TrailGroundOffset { get; }
    public ConfigEntry<float> PositionJumpThreshold { get; }
    public ConfigEntry<bool> DebugLogging { get; }

    public HomeGuidanceConfig(ConfigFile config)
    {
        Enabled = config.Bind("General", "Enabled", true,
            "Master toggle for HomeGuidance. Off hides all navigation visuals.");

        ArrivalRadius = config.Bind("Arrival", "ArrivalRadius", 2.0f,
            new ConfigDescription("Radius (m) around extraction circle to count a player as arrived.",
                new AcceptableValueRange<float>(0.5f, 6.0f)));

        ArrivalScanInterval = config.Bind("Arrival", "ArrivalScanInterval", 0.15f,
            new ConfigDescription("Interval (s) between player arrival scans.",
                new AcceptableValueRange<float>(0.10f, 0.50f)));

        RouteCheckInterval = config.Bind("Routing", "RouteCheckInterval", 0.50f,
            new ConfigDescription("Interval (s) between route validity checks.",
                new AcceptableValueRange<float>(0.20f, 2.00f)));

        RouteRetryInterval = config.Bind("Routing", "RouteRetryInterval", 0.75f,
            new ConfigDescription("Interval (s) between route retries after failure.",
                new AcceptableValueRange<float>(0.25f, 3.00f)));

        RouteDeviationDistance = config.Bind("Routing", "RouteDeviationDistance", 3.0f,
            new ConfigDescription("Distance (m) from path before triggering recalculation.",
                new AcceptableValueRange<float>(1.0f, 8.0f)));

        EstimatedWalkSpeed = config.Bind("Routing", "EstimatedWalkSpeed", 4.5f,
            new ConfigDescription("Estimated player walk speed (m/s) for route time calculation.",
                new AcceptableValueRange<float>(1.0f, 10.0f)));

        TeleportCountdownSeconds = config.Bind("Routing", "TeleportCountdownSeconds", 3.0f,
            new ConfigDescription("Fallback teleporter countdown (s).",
                new AcceptableValueRange<float>(1.0f, 10.0f)));

        TeleportWaitSeconds = config.Bind("Routing", "TeleportWaitSeconds", 0.5f,
            new ConfigDescription("Fallback teleporter wait after activation (s).",
                new AcceptableValueRange<float>(0.1f, 5.0f)));

        RouteSwitchGainSeconds = config.Bind("Routing", "RouteSwitchGainSeconds", 0.35f,
            new ConfigDescription("Minimum time gain (s) before switching route to prevent flicker.",
                new AcceptableValueRange<float>(0.0f, 2.0f)));

        LookAheadDistance = config.Bind("Arrow", "LookAheadDistance", 6.0f,
            new ConfigDescription("Distance (m) along path for arrow look-ahead point.",
                new AcceptableValueRange<float>(2.0f, 12.0f)));

        SkipNearCornerDistance = config.Bind("Arrow", "SkipNearCornerDistance", 2.0f,
            new ConfigDescription("Skip corners within this distance (m) of player.",
                new AcceptableValueRange<float>(0.25f, 5.0f)));

        VerticalEnterThreshold = config.Bind("Arrow", "VerticalEnterThreshold", 1.25f,
            new ConfigDescription("Vertical delta (m) to enter up/down state.",
                new AcceptableValueRange<float>(0.25f, 5.0f)));

        VerticalExitThreshold = config.Bind("Arrow", "VerticalExitThreshold", 0.75f,
            new ConfigDescription("Vertical delta (m) to exit up/down state. Must be <= EnterThreshold.",
                new AcceptableValueRange<float>(0.25f, 5.0f)));

        ArrowSmoothTime = config.Bind("Arrow", "ArrowSmoothTime", 0.12f,
            new ConfigDescription("SmoothDamp time (s) for arrow rotation.",
                new AcceptableValueRange<float>(0.02f, 0.50f)));

        TrailDotSpacing = config.Bind("Trail", "TrailDotSpacing", 1.5f,
            new ConfigDescription("Spacing (m) between trail dots along arc length.",
                new AcceptableValueRange<float>(0.5f, 5.0f)));

        TrailMaxDots = config.Bind("Trail", "TrailMaxDots", 96,
            new ConfigDescription("Maximum number of trail dot instances in pool.",
                new AcceptableValueRange<int>(8, 256)));

        TrailGroundOffset = config.Bind("Trail", "TrailGroundOffset", 0.10f,
            new ConfigDescription("Vertical offset (m) above ground for trail dots.",
                new AcceptableValueRange<float>(0.0f, 1.0f)));

        PositionJumpThreshold = config.Bind("General", "PositionJumpThreshold", 8.0f,
            new ConfigDescription("Distance (m) in one frame treated as teleport fallback.",
                new AcceptableValueRange<float>(4.0f, 30.0f)));

        DebugLogging = config.Bind("General", "DebugLogging", false,
            "Enable verbose debug logging for graph, hierarchy, and path details.");
    }

    // Apply clamping after bind in case config file has invalid values
    public void Normalize()
    {
        if (VerticalExitThreshold.Value > VerticalEnterThreshold.Value)
            VerticalExitThreshold.Value = VerticalEnterThreshold.Value;
    }
}
