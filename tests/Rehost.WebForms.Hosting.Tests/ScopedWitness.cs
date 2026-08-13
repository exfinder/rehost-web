namespace Rehost.WebForms.Hosting.Tests;

// The witness surface a shared host hands out: token-filtered queries only, so a test cannot
// observe other classes' traffic on the same process.
internal sealed class ScopedWitness(HostWitness witness)
{
    internal Task<string[]> StagesAsync(string token) => witness.StagesAsync(token);
}
