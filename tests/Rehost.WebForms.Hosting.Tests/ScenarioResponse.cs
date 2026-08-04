namespace Rehost.WebForms.Hosting.Tests;

internal sealed class ScenarioResponse
{
    internal required int StatusCode { get; init; }

    internal required string ReasonPhrase { get; init; }

    internal required IReadOnlyDictionary<string, string> Headers { get; init; }

    internal required byte[] Bytes { get; init; }

    internal string? ContentType => Header("Content-Type");

    internal string Text => System.Text.Encoding.UTF8.GetString(Bytes);

    internal string? Header(string name) =>
        Headers.TryGetValue(name, out var value) ? value : null;
}
