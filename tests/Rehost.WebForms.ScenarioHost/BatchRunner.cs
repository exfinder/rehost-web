using System.Text.RegularExpressions;
using System.Web;
using System.Web.Hosting;
using Rehost.WebForms.ScenarioProtocol;

namespace Rehost.WebForms.ScenarioHost;

// requests enter an activated application.
public sealed class BatchRunner : MarshalByRefObject, IRegisteredObject
{
    public int Request(string path, string? responsePath)
    {
        var request = new ScenarioWorkerRequest(path);
        HttpRuntime.ProcessRequest(request);

        if (!request.WaitForCompletion(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException("The request did not complete: " + path);
        }

        if (responsePath != null)
        {
            File.WriteAllBytes(responsePath, request.BodyBytes);
        }

        if (request.StatusCode >= 500)
        {
            TraceChannel.Record(TraceEvents.ErrorBody + Summarize(request.Body));
        }

        return request.StatusCode;
    }

    public void Stop(bool immediate)
    {
        HostingEnvironment.UnregisterObject(this);
    }

    // A compilation error page is thousands of characters of markup; the compiler diagnostics in
    // it are what a failing scenario needs to report.
    private static string Summarize(string body)
    {
        // The style block alone is longer than anything worth recording, and it sits ahead of the
        // compiler diagnostics that make a failing scenario diagnosable.
        var text = Regex.Replace(body, "<(style|script)[^>]*>.*?</\\1>", " ", RegexOptions.Singleline);
        text = Regex.Replace(text, "<[^>]+>", " ");
        text = Regex.Replace(text, @"\s+", " ").Trim();

        return text.Length > 600 ? text[..600] : text;
    }
}
