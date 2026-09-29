using System.Diagnostics;
using System.Text;

namespace Rehost.Owin.Host.SystemWeb.Tests.CallEnvironment;

internal sealed class CapturingTraceListener : TraceListener
{
    private readonly StringBuilder _captured = new();

    internal string Captured => _captured.ToString();

    public override void Write(string? message) => _captured.Append(message);

    public override void WriteLine(string? message) => _captured.AppendLine(message);
}
