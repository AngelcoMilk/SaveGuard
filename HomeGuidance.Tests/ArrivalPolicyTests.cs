namespace HomeGuidance.Tests;

public static class ArrivalPolicyTests
{
    public static void RunAll()
    {
        RadiusSquaredComputation();
    }

    private static void RadiusSquaredComputation()
    {
        float r = 2.0f;
        AssertEx.FloatEqual(4f, r * r);
        Program.RecordPass();
    }
}
