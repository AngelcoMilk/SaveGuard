namespace HomeGuidance.Tests;

public static class RouteSelectionPolicy
{
    public static bool ShouldReplace(TestRoutePlan current, TestRoutePlan candidate, float requiredGainSeconds)
    {
        if (candidate == null || !candidate.IsValid) return false;
        if (current == null || !current.IsValid) return true;
        if (candidate.Reason == RouteReplanReason.PlayerTeleported) return true;
        if (candidate.TeleportGeneration != current.TeleportGeneration) return true;
        if (candidate.RoundToken != current.RoundToken) return true;
        if (candidate.Reason == RouteReplanReason.RouteDeviation
            || candidate.Reason == RouteReplanReason.TargetChanged
            || candidate.Reason == RouteReplanReason.ExtractionChanged
            || candidate.Reason == RouteReplanReason.Initial)
            return true;
        if (candidate.TopologySignature == current.TopologySignature) return true;
        return candidate.TotalCostSeconds <= current.TotalCostSeconds - requiredGainSeconds;
    }
}
