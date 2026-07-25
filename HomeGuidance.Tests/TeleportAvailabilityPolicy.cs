using System.Collections.Generic;

namespace HomeGuidance.Tests;

public static class TeleportAvailabilityPolicy
{
    public readonly struct Evaluation
    {
        public bool Available { get; init; }
        public float IncrementalCost { get; init; }

        public static Evaluation Unavailable() => new() { Available = false };
        public static Evaluation AvailableWithCost(float cost) => new() { Available = true, IncrementalCost = cost };
    }

    public static Evaluation Evaluate(TeleportTimingSnapshot timing, float arrivalAtEntrance)
    {
        switch (timing.StateCode)
        {
            case 0: // Idle
                return Evaluation.AvailableWithCost(timing.CountdownDuration + timing.TeleportWait);

            case 1: // Activating
            {
                float currentSweepAt = (timing.CountdownSecondsLeft > 0 ? timing.CountdownSecondsLeft : 0) + timing.TeleportWait;
                if (arrivalAtEntrance <= currentSweepAt + 0.001f)
                    return Evaluation.AvailableWithCost(currentSweepAt - arrivalAtEntrance);
                return Evaluation.Unavailable();
            }

            case 2: // Finished
            default:
                return Evaluation.Unavailable();
        }
    }
}
