using System.Text;

namespace Rehost.WebForms.ScenarioProbes;

// A response filter whose effect is visible in the bytes: whatever passes through it comes out
// upper-cased, so a test can tell which writes the filter chain saw.
public sealed class UpperCaseFilter(Stream inner) : Stream
{
    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => inner.Flush();

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
    {
        var text = Encoding.UTF8.GetString(buffer, offset, count).ToUpperInvariant();
        var upper = Encoding.UTF8.GetBytes(text);
        inner.Write(upper, 0, upper.Length);
    }
}
