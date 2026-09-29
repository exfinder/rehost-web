#nullable enable

using System.Collections.Generic;
using System.Configuration;

namespace System.Web.IisConfig;

internal readonly struct IisPreCondition
{
    internal static readonly IisPreCondition Unconditional = new(satisfied: true, requiresManagedHandler: false);

    internal IisPreCondition(bool satisfied, bool requiresManagedHandler)
    {
        Satisfied = satisfied;
        RequiresManagedHandler = requiresManagedHandler;
    }

    internal bool Satisfied { get; }

    internal bool RequiresManagedHandler { get; }

    internal IisPreCondition WithoutManagedHandler() =>
        new(Satisfied, requiresManagedHandler: false);
}

// The pool the port models is permanently integrated, v4.0 and 64-bit, so those tokens are
// statically satisfied and their counterparts statically unsatisfied (MH11, MH25). managedHandler
// alone stays per-request: it is carried as a flag, never evaluated here (MH10, MH17).
internal static class IisPreConditions
{
    internal const string ManagedHandler = "managedHandler";

    private static readonly HashSet<string> SatisfiedTokens = new(StringComparer.Ordinal)
    {
        "integratedMode",
        "runtimeVersionv4.0",
        "bitness64",
    };

    private static readonly HashSet<string> UnsatisfiedTokens = new(StringComparer.Ordinal)
    {
        "classicMode",
        "runtimeVersionv1.1",
        "runtimeVersionv2.0",
        "bitness32",
    };

    internal static IisPreCondition Evaluate(
        string entryKind,
        string name,
        string? preCondition,
        string configPath)
    {
        if (string.IsNullOrEmpty(preCondition))
        {
            return IisPreCondition.Unconditional;
        }

        var satisfied = true;
        var requiresManagedHandler = false;

        foreach (var token in preCondition!.Split(','))
        {
            var trimmed = token.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (string.Equals(trimmed, ManagedHandler, StringComparison.Ordinal))
            {
                requiresManagedHandler = true;
            }
            else if (UnsatisfiedTokens.Contains(trimmed))
            {
                satisfied = false;
            }
            else if (!SatisfiedTokens.Contains(trimmed))
            {
                throw new ConfigurationErrorsException(
                    entryKind + " \"" + name + "\" in '" + configPath
                    + "' has a bad preCondition \"" + trimmed
                    + "\"; IIS refuses every request to the application with a 500.0. The"
                    + " recognized tokens are integratedMode, classicMode, managedHandler,"
                    + " runtimeVersionv1.1, runtimeVersionv2.0, runtimeVersionv4.0, bitness32"
                    + " and bitness64.");
            }
        }

        return new IisPreCondition(satisfied, requiresManagedHandler);
    }
}
