using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Rehost.WebForms.Parity.Harness;

public sealed class ParityCommandLine
{
    private ParityCommandLine()
    {
    }

    public ParityOperation Operation { get; private set; }

    public string? OutputPath { get; private set; }

    public string? ExpectedPath { get; private set; }

    public string? ManifestPath { get; private set; }

    public string? FixtureRoot { get; private set; }

    public string? SessionName { get; private set; }

    public static ParityCommandLine Parse(
        string[] args,
        IReadOnlyCollection<ParityOperation> supported,
        string executableName)
    {
        var result = new ParityCommandLine();
        var index = 0;

        if (index < args.Length && !args[index].StartsWith("--", StringComparison.Ordinal))
        {
            result.Operation = args[index] switch
            {
                "run" => ParityOperation.Run,
                "generate" => ParityOperation.Generate,
                "verify" => ParityOperation.Verify,
                "run-session" => ParityOperation.RunSession,
                _ => throw Usage(supported, executableName, "Unknown command '" + args[index] + "'.")
            };
            index++;
        }

        if (!supported.Contains(result.Operation))
        {
            throw Usage(
                supported,
                executableName,
                "Command '" + result.Operation + "' is not supported by this host.");
        }

        while (index < args.Length)
        {
            var option = args[index++];

            if (index >= args.Length)
            {
                throw Usage(supported, executableName, "Missing value for '" + option + "'.");
            }

            var value = args[index++];
            switch (option)
            {
                case "--output":
                    Require(result.Operation, ParityOperation.Generate, option, supported, executableName);
                    result.OutputPath = value;
                    break;
                case "--expected":
                    Require(result.Operation, ParityOperation.Verify, option, supported, executableName);
                    result.ExpectedPath = value;
                    break;
                case "--manifest":
                    result.ManifestPath = value;
                    break;
                case "--fixtures":
                    result.FixtureRoot = value;
                    break;
                case "--session":
                    Require(result.Operation, ParityOperation.RunSession, option, supported, executableName);
                    result.SessionName = value;
                    break;
                default:
                    throw Usage(supported, executableName, "Unknown option '" + option + "'.");
            }
        }

        if (result.Operation == ParityOperation.Generate
            && string.IsNullOrWhiteSpace(result.OutputPath))
        {
            throw Usage(supported, executableName, "generate requires --output <path>.");
        }

        if (result.Operation == ParityOperation.RunSession
            && string.IsNullOrWhiteSpace(result.SessionName))
        {
            throw Usage(supported, executableName, "run-session requires --session <name>.");
        }

        return result;
    }

    private static void Require(
        ParityOperation actual,
        ParityOperation required,
        string option,
        IReadOnlyCollection<ParityOperation> supported,
        string executableName)
    {
        if (actual != required)
        {
            throw Usage(
                supported,
                executableName,
                "'" + option + "' is not valid for this command.");
        }
    }

    private static ArgumentException Usage(
        IReadOnlyCollection<ParityOperation> supported,
        string executableName,
        string message)
    {
        var builder = new StringBuilder(message);
        builder.AppendLine();
        builder.Append("Usage:");

        if (supported.Contains(ParityOperation.Run))
        {
            builder.AppendLine();
            builder.Append("  " + executableName + " run [--manifest <path>] [--fixtures <path>]");
        }

        if (supported.Contains(ParityOperation.Generate))
        {
            builder.AppendLine();
            builder.Append(
                "  " + executableName + " generate --output <path>"
                + " [--manifest <path>] [--fixtures <path>]");
        }

        if (supported.Contains(ParityOperation.Verify))
        {
            builder.AppendLine();
            builder.Append(
                "  " + executableName + " verify [--expected <path>]"
                + " [--manifest <path>] [--fixtures <path>]");
        }

        if (supported.Contains(ParityOperation.RunSession))
        {
            builder.AppendLine();
            builder.Append(
                "  " + executableName + " run-session --session <name>"
                + " [--manifest <path>] [--fixtures <path>]");
        }

        return new ArgumentException(builder.ToString());
    }
}
