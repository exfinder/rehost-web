namespace Rehost.Web.AspNetCore;

using System.Text;

// IIS handed ASP.NET a request header value decoded as UTF-8 when the bytes were valid UTF-8 and
// as Latin-1 otherwise, and never refused one (reading R5: "caf\xC3\xA9" and "caf\xE9" both read
// café, "\xFF\xFE" read ÿþ). Kestrel's default is strict UTF-8 with a 400 for anything else; this
// is the selector that gives it IIS's rule. Only decoding is reached — Kestrel never encodes a
// request header — so the encoding side is UTF-8's.
internal sealed class RequestHeaderEncoding : Encoding
{
    internal static readonly RequestHeaderEncoding Instance = new();

    private static readonly Encoding StrictUtf8 = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private RequestHeaderEncoding()
    {
    }

    public override int GetCharCount(byte[] bytes, int index, int count)
    {
        return Decoder(bytes, index, count).GetCharCount(bytes, index, count);
    }

    public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
    {
        return Decoder(bytes, byteIndex, byteCount).GetChars(bytes, byteIndex, byteCount, chars, charIndex);
    }

    public override int GetMaxCharCount(int byteCount) => byteCount;

    public override int GetByteCount(char[] chars, int index, int count) =>
        UTF8.GetByteCount(chars, index, count);

    public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex) =>
        UTF8.GetBytes(chars, charIndex, charCount, bytes, byteIndex);

    public override int GetMaxByteCount(int charCount) => UTF8.GetMaxByteCount(charCount);

    private static Encoding Decoder(byte[] bytes, int index, int count)
    {
        try
        {
            StrictUtf8.GetCharCount(bytes, index, count);
            return StrictUtf8;
        }
        catch (DecoderFallbackException)
        {
            return Latin1;
        }
    }
}
