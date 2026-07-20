#nullable enable

using System;
using System.IO;
using System.Security.Principal;

namespace System.Web.Configuration;

internal sealed class RemoteWebConfigurationHostStream : Stream
{
    private const string Message = "Remote web configuration is not supported by Rehost.WebForms.";

    internal RemoteWebConfigurationHostStream(
        bool streamForWrite,
        string serverName,
        string streamName,
        string? templateStreamName,
        string? username,
        string? domain,
        string? password,
        WindowsIdentity? identity) => throw new PlatformNotSupportedException(Message);

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new PlatformNotSupportedException(Message);

    public override long Position
    {
        get => throw new PlatformNotSupportedException(Message);
        set => throw new PlatformNotSupportedException(Message);
    }

    internal void FlushForWriteCompleted() => throw new PlatformNotSupportedException(Message);

    public override void Flush() => throw new PlatformNotSupportedException(Message);

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new PlatformNotSupportedException(Message);

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new PlatformNotSupportedException(Message);

    public override void SetLength(long value) => throw new PlatformNotSupportedException(Message);

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new PlatformNotSupportedException(Message);
}
