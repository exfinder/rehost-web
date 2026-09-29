using System;
using Rehost.Web.Parity.Contracts;

namespace Rehost.Web.Parity.Harness;

public static class DiagnosticWriter
{
    public static void Write(string phase, Exception exception)
    {
        var diagnostic = new FailureDiagnostic
        {
            SchemaVersion = 1,
            Phase = phase,
            Exception = ExceptionObservation.FromException(exception)
        };
        Console.Error.WriteLine(ParityJson.Serialize(diagnostic));
    }
}
