namespace Rehost.WebForms.Hosting.Tests;

internal sealed record MultipartFile(
    string Name,
    string FileName,
    string ContentType,
    byte[] Content);
