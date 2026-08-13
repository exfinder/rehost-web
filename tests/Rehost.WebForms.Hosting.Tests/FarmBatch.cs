namespace Rehost.WebForms.Hosting.Tests;

public sealed class FarmBatch : IDisposable
{
    internal BatchRun Run { get; } = BatchRun.Farm(
        "captured",
        "captured-without-event-validation");

    public void Dispose()
    {
        Run.Dispose();
    }
}
