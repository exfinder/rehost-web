<%@ Page Language="C#" Async="true" %>
<%@ Import Namespace="System.Threading.Tasks" %>
<script runat="server">
    protected void Page_Load(object sender, EventArgs e) {
        RegisterAsyncTask(new PageAsyncTask(RunAsync));
    }

    private async Task RunAsync() {
        await Task.Delay(50);
        Response.Write("before-end|");
        Response.End();
        Response.Write("after-end");
    }
</script>
tail
