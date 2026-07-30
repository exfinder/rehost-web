<%@ Application Language="C#" %>
<script runat="server">

    void Application_Start(object sender, EventArgs e)
    {
        Rehost.WebForms.ScenarioProbes.ScenarioJournal.Record(
            "application-start:" + GetType().Assembly.GetName().Name);
        CodegenProbe.RecordCompiled();
    }

    void Application_BeginRequest(object sender, EventArgs e)
    {
        Rehost.WebForms.ScenarioProbes.ScenarioJournal.Record("begin-request");
    }

</script>
