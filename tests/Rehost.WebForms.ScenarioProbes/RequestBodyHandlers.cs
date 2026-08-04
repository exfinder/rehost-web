using System.Reflection;
using System.Security.Cryptography;
using System.Web;

namespace Rehost.WebForms.ScenarioProbes;

public sealed class RequestBodyHandler : IHttpHandler
{
    public bool IsReusable => false;

    public void ProcessRequest(HttpContext context)
    {
        var mode = context.Request.QueryString["mode"];
        byte[] body;

        WitnessJournal.Record("handler-entered:" + mode);
        ScenarioJournal.Record("handler-entered:" + mode);

        switch (mode)
        {
            case "abort-apm":
                ReadUntilApmFailure(context);
                return;
            case "abort":
                try
                {
                    ReadAll(context.Request.GetBufferlessInputStream());
                }
                catch (HttpException exception)
                {
                    WitnessJournal.Record("body-abort:" + exception.GetType().FullName);
                    ScenarioJournal.Record("body-abort:" + exception.GetType().FullName);
                }
                return;
            case "unread":
                WriteResult(context, "unread");
                return;
            case "binary":
                body = context.Request.BinaryRead(context.Request.TotalBytes);
                break;
            case "apm":
                body = ReadAllApm(context);
                break;
            case "buffered":
                body = ReadAll(context.Request.GetBufferedInputStream());
                break;
            case "bufferless":
                body = ReadAll(context.Request.GetBufferlessInputStream());
                break;
            case "preload":
                context.Response.AddHeader("X-Read-Mode", context.Request.ReadEntityBodyMode.ToString());
                body = ReadAll(context.Request.InputStream);
                break;
            default:
                body = ReadAll(context.Request.InputStream);
                break;
        }

        if (mode == "spill")
        {
            context.Response.AddHeader("X-Spilled", IsFileBacked(context.Request.InputStream).ToString());
        }

        WriteResult(context, Describe(body));
    }

    private static bool IsFileBacked(Stream stream)
    {
        var data = stream.GetType()
            .GetField("_data", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(stream)!;
        return data.GetType()
            .GetField("_file", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(data) != null;
    }

    internal static byte[] ReadAll(Stream stream)
    {
        using var body = new MemoryStream();
        stream.CopyTo(body);
        return body.ToArray();
    }

    private static byte[] ReadAllApm(HttpContext context)
    {
        using var stream = context.Request.GetBufferlessInputStream();
        using var body = new MemoryStream();
        var buffer = new byte[3];

        while (true)
        {
            var read = stream.BeginRead(buffer, 0, buffer.Length, null, null);
            var count = stream.EndRead(read);
            if (count == 0)
            {
                return body.ToArray();
            }

            body.Write(buffer, 0, count);
        }
    }

    private static void ReadUntilApmFailure(HttpContext context)
    {
        var workerRequest = (HttpWorkerRequest)typeof(HttpContext)
            .GetProperty("WorkerRequest", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(context)!;
        var buffer = new byte[3];

        try
        {
            while (workerRequest.EndRead(
                workerRequest.BeginRead(buffer, 0, buffer.Length, null, null)) != 0)
            {
            }
        }
        catch (HttpException exception)
        {
            WitnessJournal.Record("body-apm-abort:" + exception.GetType().FullName);
            ScenarioJournal.Record("body-apm-abort:" + exception.GetType().FullName);
        }
    }

    internal static string Describe(byte[] body) =>
        body.Length + ":" + Convert.ToHexString(SHA256.HashData(body));

    internal static void WriteResult(HttpContext context, string value)
    {
        context.Response.StatusCode = 200;
        context.Response.ContentType = "text/plain";
        context.Response.AddHeader("X-Remote-Port", context.Request.ServerVariables["REMOTE_PORT"]);
        context.Response.Write(value);
    }
}
