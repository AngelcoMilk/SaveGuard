using System;

namespace HomeGuidance.Tests;

public static class Program
{
    private static int _passed;
    private static int _failed;
    private static int _currentGroupPassed;
    private static int _currentGroupFailed;

    public static int Main()
    {
        Console.WriteLine("HomeGuidance.Tests — running...");

        RunTestGroup("DijkstraSolver", DijkstraSolverTests.RunAll);
        RunTestGroup("TeleportAvailabilityPolicy", TeleportAvailabilityPolicyTests.RunAll);
        RunTestGroup("RouteSelectionPolicy", RouteSelectionPolicyTests.RunAll);
        RunTestGroup("RoundGuidanceState", RoundGuidanceStateTests.RunAll);
        RunTestGroup("ArrivalPolicy", ArrivalPolicyTests.RunAll);

        Console.WriteLine($"\n=== Results: {_passed} passed, {_failed} failed ===");
        return _failed > 0 ? 1 : 0;
    }

    private static void RunTestGroup(string name, Action tests)
    {
        _currentGroupPassed = 0;
        _currentGroupFailed = 0;

        try
        {
            tests();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  {name}: GROUP EXCEPTION — {ex.Message}");
            _failed++;
            return;
        }

        Console.WriteLine($"  {name}: {_currentGroupPassed} passed, {_currentGroupFailed} failed");
    }

    public static void RecordPass()
    {
        _passed++;
        _currentGroupPassed++;
    }

    public static void RecordFail(string message)
    {
        _failed++;
        _currentGroupFailed++;
        Console.WriteLine($"    FAIL: {message}");
    }
}
