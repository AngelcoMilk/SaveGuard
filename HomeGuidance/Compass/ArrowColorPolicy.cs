using UnityEngine;

namespace HomeGuidance.Compass;

/// <summary>
/// Arrow color with dual-threshold hysteresis to prevent flicker near height boundaries.
/// Color priority: Teleport purple > Up blue > Down red > Level white.
/// Hysteresis: separate enter/exit thresholds prevent rapid toggling.
/// </summary>
public sealed class ArrowColorState
{
    private enum VerticalState { Level, Up, Down }
    private VerticalState _state = VerticalState.Level;

    public Color Update(bool isTeleportEntrance, float deltaY)
    {
        float enter = Plugin.ModConfig.VerticalEnterThreshold.Value;
        float exit = Plugin.ModConfig.VerticalExitThreshold.Value;

        // Hysteresis transitions
        switch (_state)
        {
            case VerticalState.Level:
                if (deltaY >= enter) _state = VerticalState.Up;
                else if (deltaY <= -enter) _state = VerticalState.Down;
                break;
            case VerticalState.Up:
                if (deltaY <= exit) _state = VerticalState.Level;
                break;
            case VerticalState.Down:
                if (deltaY >= -exit) _state = VerticalState.Level;
                break;
        }

        return isTeleportEntrance ? ArrowColorPolicy.TeleportPurple
            : _state == VerticalState.Up ? ArrowColorPolicy.UpBlue
            : _state == VerticalState.Down ? ArrowColorPolicy.DownRed
            : ArrowColorPolicy.LevelWhite;
    }
}

/// <summary>
/// Color constants and single-shot color determination without hysteresis.
/// For hysteresis, use ArrowColorState instead.
/// </summary>
public static class ArrowColorPolicy
{
    // Single-shot without hysteresis (legacy / simple use)
    public static Color DetermineColor(bool isTeleportEntrance, float deltaY)
    {
        if (isTeleportEntrance) return TeleportPurple;

        float enter = Plugin.ModConfig.VerticalEnterThreshold.Value;
        if (deltaY >= enter) return UpBlue;
        if (deltaY <= -enter) return DownRed;
        return LevelWhite;
    }

    public static readonly Color LevelWhite = new(1f, 1f, 1f, 1f);
    public static readonly Color UpBlue = new(0.3f, 0.6f, 1f, 1f);
    public static readonly Color DownRed = new(1f, 0.3f, 0.3f, 1f);
    public static readonly Color TeleportPurple = new(0.7f, 0.3f, 1f, 1f);
}
