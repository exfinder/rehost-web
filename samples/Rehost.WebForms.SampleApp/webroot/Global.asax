<%@ Application Language="C#" %>
<script runat="server">

    protected void Application_Start(object sender, EventArgs e)
    {
        Application["started"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    protected void Application_BeginRequest(object sender, EventArgs e)
    {
        Sample.SampleTrace.Record(Context, "BeginRequest");
    }

    protected void Application_EndRequest(object sender, EventArgs e)
    {
        Sample.SampleTrace.Record(Context, "EndRequest");
    }

</script>
