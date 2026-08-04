using System;
using System.IO;
using System.Text;
using System.Web;

namespace CoreParity.Probes;

public sealed class BodyProbeHandler : IHttpHandler
{
    public bool IsReusable => false;

    public void ProcessRequest(HttpContext context)
    {
        var mode = context.Request.QueryString["mode"];
        byte[] body;

        switch (mode)
        {
            case "binary":
                body = context.Request.BinaryRead(context.Request.TotalBytes);
                break;
            case "buffered":
                body = ReadAll(context.Request.GetBufferedInputStream());
                break;
            case "bufferless":
                body = ReadAll(context.Request.GetBufferlessInputStream());
                break;
            case "bufferless-apm":
                body = ReadAllApm(context.Request.GetBufferlessInputStream());
                break;
            default:
                body = ReadAll(context.Request.InputStream);
                break;
        }

        var result = Encoding.UTF8.GetBytes(
            context.Request.ReadEntityBodyMode
                + ":"
                + Convert.ToBase64String(body));
        context.Response.StatusCode = 200;
        context.Response.ContentType = "text/plain; charset=utf-8";
        context.Response.OutputStream.Write(result, 0, result.Length);
    }

    private static byte[] ReadAll(Stream stream)
    {
        using (var result = new MemoryStream())
        {
            stream.CopyTo(result);
            return result.ToArray();
        }
    }

    private static byte[] ReadAllApm(Stream stream)
    {
        using (var result = new MemoryStream())
        {
            var buffer = new byte[3];
            while (true)
            {
                var read = stream.BeginRead(buffer, 0, buffer.Length, null, null);
                var count = stream.EndRead(read);
                if (count == 0)
                {
                    return result.ToArray();
                }

                result.Write(buffer, 0, count);
            }
        }
    }
}
