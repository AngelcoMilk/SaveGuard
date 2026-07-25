namespace HomeGuidance.Routing;

/// <summary>
/// Pure C# route selection with hysteresis.
/// Linkable into HomeGuidance.Tests.
/// </summary>
public static class RouteSelectionPolicy
{
    public static bool ShouldReplace(RoutePlan current, RoutePlan candidate, float requiredGainSeconds)
    {
        if (candidate == null || !candidate.IsValid) return false;

        // No current plan -> accept
        if (current == null || !current.IsValid) return true;

        // Teleport happened -> force replace
        if (candidate.Reason == RouteReplanReason.PlayerTeleported) return true;

        // Different generation -> force replace
        if (candidate.TeleportGeneration != current.TeleportGeneration) return true;
        if (candidate.RoundToken != current.RoundToken) return true;

        // Deviation, target change, invalid current -> immediate replace
        if (candidate.Reason == RouteReplanReason.RouteDeviation
            || candidate.Reason == RouteReplanReason.TargetChanged
            || candidate.Reason == RouteReplanReason.ExtractionChanged
            || candidate.Reason == RouteReplanReason.Initial)
            return true;

        // Same topology -> allow refresh
        if (candidate.TopologySignature == current.TopologySignature) return true;

        // Different topology -> only replace if significantly better
        return candidate.TotalCostSeconds <= current.TotalCostSeconds - requiredGainSeconds;
    }
}
