using System;

namespace Rehost.WebForms.Parity.Harness;

public sealed class PhaseException : Exception
{
    public PhaseException(string phase, Exception innerException)
        : base("Parity phase failed: " + phase + ".", innerException)
    {
        Phase = phase;
    }

    public string Phase { get; }
}
