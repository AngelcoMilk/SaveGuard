using UnityEngine;

namespace HomeGuidance.Runtime;

public sealed class GuidanceSnapshot
{
    public bool RoundActive;
    public int LocalPawnInstanceId;
    public uint LocalPawnNetId;
    public bool LocalPawnAlive;
    public bool LocalPawnExtracted;
    public int ExtractionInstanceId;
    public Vector3 ExtractionPosition;
    public int CompassInstanceId;
    public bool CompassSettingEnabled;
    public int TeleporterTopologyHash;
    public int TeleporterCostHash;

    public bool CompassChanged(GuidanceSnapshot prev)
    {
        if (prev == null) return true;
        return CompassInstanceId != prev.CompassInstanceId
            || CompassSettingEnabled != prev.CompassSettingEnabled;
    }

    public bool DifferentFrom(GuidanceSnapshot prev)
    {
        if (prev == null) return true;
        return RoundActive != prev.RoundActive
            || LocalPawnInstanceId != prev.LocalPawnInstanceId
            || ExtractionInstanceId != prev.ExtractionInstanceId
            || CompassInstanceId != prev.CompassInstanceId
            || CompassSettingEnabled != prev.CompassSettingEnabled
            || TeleporterTopologyHash != prev.TeleporterTopologyHash
            || TeleporterCostHash != prev.TeleporterCostHash;
    }
}
