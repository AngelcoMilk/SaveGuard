using System;

namespace HomeGuidance.Runtime;

[Flags]
public enum GuidanceDirtyFlags
{
    None                = 0,
    RoundChanged        = 1 << 0,
    LocalPawnChanged    = 1 << 1,
    ExtractionChanged   = 1 << 2,
    ArrivalStateChanged = 1 << 3,
    GraphTopologyChanged = 1 << 4,
    TeleportCostChanged  = 1 << 5,
    PlayerTeleported     = 1 << 6,
    RouteDeviation       = 1 << 7,
    RouteInvalid         = 1 << 8,
    CompassChanged       = 1 << 9,
    ConfigChanged        = 1 << 10,
    RouteRequested       = 1 << 11
}
