using UnityEngine;

namespace HomeGuidance.Routing;

public sealed class TeleporterSnapshot
{
    public int StableId;
    public Vector3 SourcePosition;
    public int PairedStableId;
    public bool HasPaired;
    public bool IsInExtractionMode;
    public int StateCode;
    public int CountdownSecondsLeft;
    public float CountdownDuration;
    public float TeleportWait;
    public int ExtractionInstanceId;
}
