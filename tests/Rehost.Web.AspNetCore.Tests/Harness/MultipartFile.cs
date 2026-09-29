namespace Rehost.Web.AspNetCore.Tests;

internal sealed record MultipartFile(
    string Name,
    string FileName,
    string ContentType,
    byte[] Content);
