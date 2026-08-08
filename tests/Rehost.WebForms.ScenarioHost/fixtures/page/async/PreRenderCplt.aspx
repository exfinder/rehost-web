<%@ Page Language="C#" Async="true" %>
<%@ Import Namespace="System.Collections.Generic" %>
<%@ Import Namespace="System.Threading" %>
<%@ Import Namespace="System.Threading.Tasks" %>
<script runat="server">
    private readonly List<string> _lines = new List<string>();
    private readonly object _gate = new object();
    private HttpContext _loadContext;
    private int _loadThreadId;

    protected void Page_Load(object sender, EventArgs e) {
        _loadContext = HttpContext.Current;
        _loadThreadId = Environment.CurrentManagedThreadId;
        Observe("load");
        AddOnPreRenderCompleteAsync(BeginWork, EndWork);
    }

    private IAsyncResult BeginWork(object sender, EventArgs e, AsyncCallback callback, object state) {
        Observe("begin");
        var completion = new TaskCompletionSource<bool>(state);
        Task.Delay(120).ContinueWith(delegate {
            completion.SetResult(true);
            if (callback != null) {
                callback(completion.Task);
            }
        });
        return completion.Task;
    }

    private void EndWork(IAsyncResult result) {
        Observe("end");
    }

    private void Observe(string stage) {
        var context = HttpContext.Current;
        var syncContext = SynchronizationContext.Current;
        var line = stage
            + "[tid=" + (Environment.CurrentManagedThreadId == _loadThreadId ? "load" : "other")
            + ";sc=" + (syncContext == null ? "null" : syncContext.GetType().Name)
            + ";ctx=" + (context == null ? "null" : ReferenceEquals(context, _loadContext) ? "same" : "other")
            + "]";
        lock (_gate) {
            _lines.Add(line);
        }
    }

    protected string Report() {
        Observe("render");
        lock (_gate) {
            return string.Join("|", _lines);
        }
    }
</script>
<%= Report() %>
