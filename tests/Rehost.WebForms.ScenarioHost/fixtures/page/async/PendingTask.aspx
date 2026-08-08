<%@ Page Language="C#" Async="true" %>
<%@ Import Namespace="System.Collections.Generic" %>
<%@ Import Namespace="System.Threading" %>
<%@ Import Namespace="System.Threading.Tasks" %>
<script runat="server">
    private readonly List<string> _lines = new List<string>();
    private HttpContext _loadContext;
    private int _loadThreadId;

    protected void Page_Load(object sender, EventArgs e) {
        _loadContext = HttpContext.Current;
        _loadThreadId = Environment.CurrentManagedThreadId;
        Observe("load");
        RegisterAsyncTask(new PageAsyncTask(RunAsync));
    }

    private async Task RunAsync() {
        Observe("task-begin");
        await Task.Delay(150);
        Observe("after-await");
        await Task.Delay(50).ConfigureAwait(false);
        Observe("after-caf");
    }

    private void Observe(string stage) {
        var context = HttpContext.Current;
        var syncContext = SynchronizationContext.Current;
        _lines.Add(stage
            + "[tid=" + (Environment.CurrentManagedThreadId == _loadThreadId ? "load" : "other")
            + ";sc=" + (syncContext == null ? "null" : syncContext.GetType().Name)
            + ";ctx=" + (context == null ? "null" : ReferenceEquals(context, _loadContext) ? "same" : "other")
            + "]");
    }

    protected string Report() {
        Observe("render");
        return string.Join("|", _lines);
    }
</script>
<%= Report() %>
