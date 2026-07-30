using System.Text;
using System.Web;

namespace Rehost.WebForms.ScenarioHost;

// The smallest worker request that reaches the pipeline. Scenarios here assert on what the
// application did, not on what it wrote, so the response is captured only to prove the request
// completed and to surface an error page when one is produced.
internal sealed class ScenarioWorkerRequest : HttpWorkerRequest
{
    private readonly string _rawUrl;
    private readonly string _path;
    private readonly string _queryString;
    private readonly MemoryStream _body = new();
    private readonly ManualResetEventSlim _completed = new(false);

    internal ScenarioWorkerRequest(string rawUrl)
    {
        _rawUrl = rawUrl;

        var separator = rawUrl.IndexOf('?');
        _path = separator < 0 ? rawUrl : rawUrl[..separator];
        _queryString = separator < 0 ? string.Empty : rawUrl[(separator + 1)..];
    }

    internal int StatusCode { get; private set; } = 200;

    internal string Body => Encoding.UTF8.GetString(_body.ToArray());

    internal byte[] BodyBytes => _body.ToArray();

    internal bool WaitForCompletion(TimeSpan timeout) => _completed.Wait(timeout);

    public override string GetUriPath() => _path;

    public override string GetQueryString() => _queryString;

    public override string GetRawUrl() => _rawUrl;

    public override string GetHttpVerbName() => "GET";

    public override string GetHttpVersion() => "HTTP/1.1";

    public override string GetRemoteAddress() => "127.0.0.1";

    public override int GetRemotePort() => 49152;

    public override string GetLocalAddress() => "127.0.0.1";

    public override int GetLocalPort() => 80;

    public override string GetFilePath() => _path;

    public override string GetFilePathTranslated()
    {
        var relative = _path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(GetAppPathTranslated(), relative);
    }

    public override string GetPathInfo() => string.Empty;

    public override string GetAppPath() => "/";

    public override string GetAppPathTranslated() =>
        HttpRuntime.AppDomainAppPath ?? AppDomain.CurrentDomain.BaseDirectory;

    public override void SendStatus(int statusCode, string statusDescription)
    {
        StatusCode = statusCode;
    }

    public override void SendKnownResponseHeader(int index, string value)
    {
    }

    public override void SendUnknownResponseHeader(string name, string value)
    {
    }

    public override void SendResponseFromMemory(byte[] data, int length)
    {
        _body.Write(data, 0, length);
    }

    public override void SendResponseFromFile(string filename, long offset, long length)
    {
    }

    public override void SendResponseFromFile(IntPtr handle, long offset, long length)
    {
    }

    public override void FlushResponse(bool finalFlush)
    {
    }

    public override void EndOfRequest()
    {
        _completed.Set();
    }
}
