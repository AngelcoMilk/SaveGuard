using System;
using System.Runtime.CompilerServices;

namespace HomeGuidance.Tests;

public static class AssertEx
{
    public static void Assert(bool condition, string message = null,
        [CallerLineNumber] int line = 0)
    {
        if (!condition)
            throw new Exception($"Assertion failed at line {line}: {message ?? "condition was false"}");
    }

    public static void Equal<T>(T expected, T actual, string message = null,
        [CallerLineNumber] int line = 0) where T : IEquatable<T>
    {
        if (!expected.Equals(actual))
            throw new Exception($"Expected {expected} but got {actual} at line {line}: {message}");
    }

    public static void FloatEqual(float expected, float actual, float epsilon = 0.001f,
        string message = null, [CallerLineNumber] int line = 0)
    {
        if (Math.Abs(expected - actual) > epsilon)
            throw new Exception($"Expected ~{expected} but got {actual} at line {line}: {message}");
    }

    public static void NotNull(object obj, string message = null,
        [CallerLineNumber] int line = 0)
    {
        if (obj == null)
            throw new Exception($"Expected non-null at line {line}: {message}");
    }

    public static void True(bool condition, string message = null,
        [CallerLineNumber] int line = 0)
    {
        Assert(condition, message, line);
    }

    public static void False(bool condition, string message = null,
        [CallerLineNumber] int line = 0)
    {
        Assert(!condition, message, line);
    }
}
