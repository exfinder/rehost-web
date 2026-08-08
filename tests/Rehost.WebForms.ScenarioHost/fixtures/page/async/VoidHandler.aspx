<%@ Page Language="C#" Async="true" %>
<%@ Import Namespace="System.Collections.Concurrent" %>
<%@ Import Namespace="System.Threading" %>
<%@ Import Namespace="System.Threading.Tasks" %>
<script runat="server">
    // Lines survive past render in a static so a follow-up ?dump=<k> request can read
    // observations the async void continuation records after the response is gone.
    private static readonly ConcurrentDictionary<string, ConcurrentQueue<string>> Recorded =
        new ConcurrentDictionary<string, ConcurrentQueue<string>>();

    private HttpContext _loadContext;
    private int _loadThreadId;
    private string _key;

    protected async void Page_Load(object sender, EventArgs e) {
        if (Request.QueryString["dump"] != null) {
            return;
        }

        _key = Request.QueryString["k"] ?? "default";
        _loadContext = HttpContext.Current;
        _loadThreadId = Environment.CurrentManagedThreadId;
        Observe("load");
        await Task.Delay(150);
        Observe("after-await");
        await Task.Delay(50).ConfigureAwait(false);
        Observe("after-caf");
    }

    private void Observe(string stage) {
        var context = HttpContext.Current;
        var syncContext = SynchronizationContext.Current;
        var line = stage
            + "[tid=" + (Environment.CurrentManagedThreadId == _loadThreadId ? "load" : "other")
            + ";sc=" + (syncContext == null ? "null" : syncContext.GetType().Name)
            + ";ctx=" + (context == null ? "null" : ReferenceEquals(context, _loadContext) ? "same" : "other")
            + "]";
        Recorded.GetOrAdd(_key, delegate { return new ConcurrentQueue<string>(); }).Enqueue(line);
    }

    protected string Report() {
        var dump = Request.QueryString["dump"];
        ConcurrentQueue<string> lines;
        if (dump != null) {
            return Recorded.TryGetValue(dump, out lines)
                ? string.Join("|", lines)
                : "no-recording";
        }

        Observe("render");
        return Recorded.TryGetValue(_key, out lines) ? string.Join("|", lines) : "empty";
    }
</script>
<%= Report() %>
