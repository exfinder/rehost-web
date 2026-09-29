using System;
using System.Text;
using System.Threading;
using System.Web;
using Rehost.Web.Parity.Contracts;

namespace Rehost.Web.Parity.Probes;

public sealed class AsyncProbeHandler : IHttpAsyncHandler
{
    private static readonly TimeSpan GateTimeout = TimeSpan.FromSeconds(5);

    public bool IsReusable => false;

    public IAsyncResult BeginProcessRequest(
        HttpContext context,
        AsyncCallback callback,
        object? state)
    {
        ProbeEvents.Record(context, "handler.begin-process-request");

        var name = ProbeEvents.NameOf(context) ?? "";
        var result = new ProbeAsyncResult(context, state);
        var completion = new Thread(() =>
        {
            if (!ParityGate.Wait(name, GateTimeout))
            {
                PipelineEvents.Record(name, "handler.gate-timeout");
            }

            result.Complete();
            callback?.Invoke(result);
        })
        {
            IsBackground = true,
            Name = "parity-async:" + name
        };

        completion.Start();
        return result;
    }

    public void EndProcessRequest(IAsyncResult result)
    {
        // The context travels on the result rather than being looked up ambiently: completion
        // runs on a thread the pipeline never touched.
        var context = ((ProbeAsyncResult)result).Context;
        ProbeEvents.Record(context, "handler.end-process-request");

        var body = Encoding.UTF8.GetBytes("async-ok");
        context.Response.StatusCode = 202;
        context.Response.StatusDescription = "Oracle Accepted";
        context.Response.ContentType = "text/plain; charset=utf-8";
        context.Response.OutputStream.Write(body, 0, body.Length);
    }

    public void ProcessRequest(HttpContext context)
    {
        throw new NotSupportedException(
            "AsyncProbeHandler is only invoked through its asynchronous entry points.");
    }

    private sealed class ProbeAsyncResult : IAsyncResult
    {
        private readonly ManualResetEvent _completed = new ManualResetEvent(false);

        internal ProbeAsyncResult(HttpContext context, object? state)
        {
            Context = context;
            AsyncState = state;
        }

        internal HttpContext Context { get; }

        public object? AsyncState { get; }

        public WaitHandle AsyncWaitHandle => _completed;

        public bool CompletedSynchronously => false;

        public bool IsCompleted { get; private set; }

        internal void Complete()
        {
            IsCompleted = true;
            _completed.Set();
        }
    }
}
