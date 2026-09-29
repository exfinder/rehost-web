<%@ Page Language="C#" Async="true" %>
<%@ Import Namespace="System.Diagnostics" %>
<%@ Import Namespace="System.Threading.Tasks" %>
<script runat="server">
    private string _result = "unset";

    protected void Page_Load(object sender, EventArgs e) {
        RegisterAsyncTask(new PageAsyncTask(RunAsync));
    }

    private async Task RunAsync() {
        var delay = Request.QueryString["ms"];
        var watch = Stopwatch.StartNew();
        await Task.Delay(delay == null ? 3500 : int.Parse(delay));
        _result = "completed[past-budget=" + (watch.ElapsedMilliseconds > 1000) + "]";
    }

    protected string Report() {
        return _result;
    }
</script>
<p id="result"><%= Report() %></p>
