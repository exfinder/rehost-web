<%@ Page Language="C#" AsyncTimeout="1" Async="true" %>
<%@ Import Namespace="System.Collections.Generic" %>
<%@ Import Namespace="System.Diagnostics" %>
<%@ Import Namespace="System.Threading" %>
<%@ Import Namespace="System.Threading.Tasks" %>
<script runat="server">
    private readonly List<string> _lines = new List<string>();
    private readonly object _gate = new object();

    protected void Page_Load(object sender, EventArgs e) {
        if (Request.QueryString["slow"] != null) {
            RegisterAsyncTask(new PageAsyncTask(RunPastTimeout));
        }
        else {
            RegisterAsyncTask(new PageAsyncTask(RunWithinTimeout));
        }
    }

    private async Task RunWithinTimeout(CancellationToken token) {
        Observe("start[canRequest=" + token.CanBeCanceled + "]");
        await Task.Delay(100, token);
        Observe("done[cancelled=" + token.IsCancellationRequested + "]");
    }

    private async Task RunPastTimeout(CancellationToken token) {
        var watch = Stopwatch.StartNew();
        Observe("start[canRequest=" + token.CanBeCanceled + "]");
        try {
            await Task.Delay(15000, token);
            Observe("done-unexpected");
        }
        catch (OperationCanceledException) {
            Observe("cancelled[within15s=" + (watch.ElapsedMilliseconds < 15000) + "]");
        }
    }

    private void Observe(string stage) {
        lock (_gate) {
            _lines.Add(stage);
        }
    }

    protected string Report() {
        lock (_gate) {
            return string.Join("|", _lines);
        }
    }
</script>
<%= Report() %>
