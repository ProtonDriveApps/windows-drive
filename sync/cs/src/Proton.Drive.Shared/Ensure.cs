using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Proton.Drive.Shared;

public static class Ensure
{
    [DebuggerHidden]
    [StackTraceHidden]
    public static T NotNull<T>([NotNull] T? arg, string paramName, string? valuePath = null)
    {
        if (arg is null)
        {
            throw new ArgumentNullException(paramName, $"{valuePath ?? paramName} value cannot be null.");
        }

        return arg;
    }

    [DebuggerHidden]
    [StackTraceHidden]
    public static string NotNullOrEmpty([NotNull] string? arg, string paramName, string? valuePath = null)
    {
        if (string.IsNullOrEmpty(arg))
        {
            throw new ArgumentException($"{valuePath ?? paramName} value cannot be null or empty.", paramName);
        }

        return arg;
    }

    [DebuggerHidden]
    [StackTraceHidden]
    public static T[] NotNullOrEmpty<T>([NotNull] T[]? arg, string paramName, string? valuePath = null)
    {
        if (arg is null || arg.Length == 0)
        {
            throw new ArgumentException($"{valuePath ?? paramName} value cannot be null or empty.", paramName);
        }

        return arg;
    }

    [DebuggerHidden]
    [StackTraceHidden]
    public static void NotNullOrEmpty<T>([NotNull] IReadOnlyCollection<T>? arg, string paramName, string? valuePath = null)
    {
        if (arg is null || arg.Count == 0)
        {
            throw new ArgumentException($"{valuePath ?? paramName} value cannot be null or empty.", paramName);
        }
    }

    [DebuggerHidden]
    [StackTraceHidden]
    public static void IsTrue([DoesNotReturnIf(false)] bool condition, string message, string? paramName = null)
    {
        if (!condition)
        {
            throw new ArgumentException(message, paramName);
        }
    }

    [DebuggerHidden]
    [StackTraceHidden]
    public static void IsFalse([DoesNotReturnIf(true)] bool condition, string message, string? paramName = null)
    {
        if (condition)
        {
            throw new ArgumentException(message, paramName);
        }
    }
}
