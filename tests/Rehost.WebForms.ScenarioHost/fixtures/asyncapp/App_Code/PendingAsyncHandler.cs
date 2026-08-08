using System;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

// A truly-pending IHttpAsyncHandler: BeginProcessRequest returns an incomplete result and the
// completion callback fires from a pool continuation, so EndProcessRequest and the pipeline's
// resume both run on the completion side rather than inline.
public class PendingAsyncHandler : IHttpAsyncHandler
{
    public bool IsReusable
    {
        get { return false; }
    }

    public void ProcessRequest(HttpContext context)
    {
        throw new NotSupportedException("Async entry only.");
    }

    public IAsyncResult BeginProcessRequest(HttpContext context, AsyncCallback callback, object extraData)
    {
        var result = new PendingResult(context, extraData, Environment.CurrentManagedThreadId);
        Task.Delay(100).ContinueWith(delegate { result.Complete(callback); });
        return result;
    }

    public void EndProcessRequest(IAsyncResult result)
    {
        var pending = (PendingResult)result;
        var context = pending.Context;
        var current = HttpContext.Current;
        context.Response.Write("handler[tid="
            + (Environment.CurrentManagedThreadId == pending.BeginThreadId ? "begin" : "other")
            + ";ctx=" + (current == null ? "null" : ReferenceEquals(current, context) ? "same" : "other")
            + ";module=" + context.Items["module"] + "]");
    }

    private sealed class PendingResult : IAsyncResult
    {
        private readonly object _state;
        private volatile bool _completed;

        internal PendingResult(HttpContext context, object state, int beginThreadId)
        {
            Context = context;
            _state = state;
            BeginThreadId = beginThreadId;
        }

        internal HttpContext Context { get; private set; }

        internal int BeginThreadId { get; private set; }

        public object AsyncState
        {
            get { return _state; }
        }

        public WaitHandle AsyncWaitHandle
        {
            get { throw new NotSupportedException(); }
        }

        public bool CompletedSynchronously
        {
            get { return false; }
        }

        public bool IsCompleted
        {
            get { return _completed; }
        }

        internal void Complete(AsyncCallback callback)
        {
            _completed = true;
            if (callback != null)
            {
                callback(this);
            }
        }
    }
}
