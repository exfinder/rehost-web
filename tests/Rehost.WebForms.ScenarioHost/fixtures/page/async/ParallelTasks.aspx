<%@ Page Language="C#" Async="true" %>
<%@ Import Namespace="System.Collections.Generic" %>
<%@ Import Namespace="System.Threading" %>
<%@ Import Namespace="System.Threading.Tasks" %>
<script runat="server">
    private readonly List<string> _lines = new List<string>();
    private readonly object _gate = new object();

    protected void Page_Load(object sender, EventArgs e) {
        var parallel = Request.QueryString["seq"] == null;
        RegisterAsyncTask(new PageAsyncTask(BeginA, EndA, null, null, parallel));
        RegisterAsyncTask(new PageAsyncTask(BeginB, EndB, null, null, parallel));
    }

    private IAsyncResult BeginWork(string name, AsyncCallback callback, object state) {
        Observe(name + "-begin");
        var completion = new TaskCompletionSource<bool>(state);
        Task.Delay(150).ContinueWith(delegate {
            completion.SetResult(true);
            if (callback != null) {
                callback(completion.Task);
            }
        });
        return completion.Task;
    }

    private IAsyncResult BeginA(object sender, EventArgs e, AsyncCallback callback, object state) {
        return BeginWork("a", callback, state);
    }

    private IAsyncResult BeginB(object sender, EventArgs e, AsyncCallback callback, object state) {
        return BeginWork("b", callback, state);
    }

    private void EndA(IAsyncResult result) {
        Observe("a-end");
    }

    private void EndB(IAsyncResult result) {
        Observe("b-end");
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
