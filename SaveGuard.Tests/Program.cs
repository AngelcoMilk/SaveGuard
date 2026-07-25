using SaveGuard;

static void Equal<T>(T expected, T actual, string name)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{name}: expected {expected}, got {actual}");
    }
}

Equal(0, SaveGuardPolicy.ClampRecoveryPercent(-10), "Clamp low");
Equal(55, SaveGuardPolicy.ClampRecoveryPercent(55), "Clamp middle");
Equal(100, SaveGuardPolicy.ClampRecoveryPercent(120), "Clamp high");
Equal(0, SaveGuardPolicy.NormalizeRecoveryPercent(-10), "Normalize below zero");
Equal(0, SaveGuardPolicy.NormalizeRecoveryPercent(12), "Normalize to zero");
Equal(25, SaveGuardPolicy.NormalizeRecoveryPercent(13), "Normalize to twenty-five");
Equal(50, SaveGuardPolicy.NormalizeRecoveryPercent(55), "Normalize to fifty");
Equal(75, SaveGuardPolicy.NormalizeRecoveryPercent(87), "Normalize to seventy-five");
Equal(100, SaveGuardPolicy.NormalizeRecoveryPercent(88), "Normalize to one hundred");
Equal(0f, SaveGuardPolicy.ToRecoveryChance(0), "Zero chance");
Equal(0.5f, SaveGuardPolicy.ToRecoveryChance(55), "Normalized half chance");
Equal(1f, SaveGuardPolicy.ToRecoveryChance(100), "Full chance");

Equal(true, SaveGuardPolicy.ShouldUseSoftReset(true, true, false), "Soft reset enabled failure");
Equal(false, SaveGuardPolicy.ShouldUseSoftReset(true, true, true), "Successful quota stays native");
Equal(false, SaveGuardPolicy.ShouldUseSoftReset(true, false, false), "Startup reset stays native");
Equal(false, SaveGuardPolicy.ShouldUseSoftReset(false, true, false), "Protection disabled stays native");

Equal(true, SaveGuardPolicy.ShouldSuppressGameOverDelete(true, true, true), "Scoped Game Over call-site delete suppression");
Equal(false, SaveGuardPolicy.ShouldSuppressGameOverDelete(true, true, false), "Manual deletion unaffected");
Equal(false, SaveGuardPolicy.ShouldSuppressGameOverDelete(true, false, true), "Other game over unaffected");
Equal(false, SaveGuardPolicy.ShouldSuppressGameOverDelete(false, true, true), "Protection disabled delete native");

Console.WriteLine("SaveGuard policy tests passed.");
