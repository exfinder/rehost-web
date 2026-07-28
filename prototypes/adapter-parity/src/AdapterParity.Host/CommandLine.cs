using System;

namespace AdapterParity.Host;

internal enum Operation
{
    Run,
    Verify,
    RunSession,
}

internal sealed class CommandLine
{
    private CommandLine(Operation operation)
    {
        Operation = operation;
    }

    internal Operation Operation { get; }

    internal string? SessionName { get; private set; }

    internal string? ManifestPath { get; private set; }

    internal string? FixtureRoot { get; private set; }

    internal string? ExpectedPath { get; private set; }

    internal string? NormalizationPath { get; private set; }

    internal static CommandLine Parse(string[] args)
    {
        if (args.Length == 0)
        {
            throw new ArgumentException("Expected 'run', 'verify', or 'run-session'.");
        }

        var command = new CommandLine(args[0] switch
        {
            "run" => Operation.Run,
            "verify" => Operation.Verify,
            "run-session" => Operation.RunSession,
            _ => throw new ArgumentException("Unknown command '" + args[0] + "'."),
        });

        for (var index = 1; index < args.Length; index += 2)
        {
            if (index + 1 >= args.Length)
            {
                throw new ArgumentException("Option '" + args[index] + "' expects a value.");
            }

            var value = args[index + 1];
            switch (args[index])
            {
                case "--session":
                    command.SessionName = value;
                    break;
                case "--manifest":
                    command.ManifestPath = value;
                    break;
                case "--fixtures":
                    command.FixtureRoot = value;
                    break;
                case "--expected":
                    command.ExpectedPath = value;
                    break;
                case "--normalization":
                    command.NormalizationPath = value;
                    break;
                default:
                    throw new ArgumentException("Unknown option '" + args[index] + "'.");
            }
        }

        if (command.Operation == Operation.RunSession
            && string.IsNullOrEmpty(command.SessionName))
        {
            throw new ArgumentException("'run-session' requires --session.");
        }

        return command;
    }
}
