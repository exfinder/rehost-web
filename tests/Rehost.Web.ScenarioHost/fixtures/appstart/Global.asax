<%@ Application Language="C#" %>
<%@ Import Namespace="Rehost.Web.ScenarioProbes" %>
<script runat="server">

    void Application_Start(object sender, EventArgs e)
    {
        LifecycleProbe.Note("application-start");
        ShutdownProbe.Arm();
        AppStartProbe.Enter();
    }

    public override void Init()
    {
        LifecycleProbe.Note("application-init");
        base.Init();
    }

</script>
