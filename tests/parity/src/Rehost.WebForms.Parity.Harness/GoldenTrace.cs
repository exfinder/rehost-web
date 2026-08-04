using System.IO;
using Rehost.WebForms.Parity.Contracts;

namespace Rehost.WebForms.Parity.Harness;

public static class GoldenTrace
{
    public static PipelineTrace Load(string expectedPath)
    {
        expectedPath = Path.GetFullPath(expectedPath);

        if (!File.Exists(expectedPath))
        {
            throw new FileNotFoundException(
                "Golden trace is absent. Generate it on Windows; do not hand-author it.",
                expectedPath);
        }

        return ParityJson.Deserialize<PipelineTrace>(File.ReadAllText(expectedPath))
            ?? throw new InvalidDataException("Framework golden trace deserialized to null.");
    }

    public static void Verify(string expectedPath, PipelineTrace actual, TraceComparison comparison)
    {
        var expected = Load(expectedPath);

        if (expected.SchemaVersion != actual.SchemaVersion)
        {
            throw new System.InvalidOperationException(
                "Parity mismatch at $.SchemaVersion: expected "
                + expected.SchemaVersion
                + ", actual "
                + actual.SchemaVersion
                + ".");
        }

        TraceComparer.VerifySessions(expected.Sessions, actual.Sessions, comparison);
    }
}
