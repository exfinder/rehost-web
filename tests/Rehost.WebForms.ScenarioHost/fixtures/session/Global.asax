<%@ Application Language="C#" %>
<%@ Import Namespace="Rehost.WebForms.ScenarioProbes" %>
<%@ Import Namespace="Rehost.WebForms.ScenarioProtocol" %>
<script runat="server">

    void Session_Start(object sender, EventArgs e)
    {
        object seen = Session[SessionReport.StartCountKey];
        Session[SessionReport.StartCountKey] = seen == null ? 1 : ((int)seen) + 1;
    }

    void Session_End(object sender, EventArgs e)
    {
        string id;
        try
        {
            id = Session.SessionID;
        }
        catch (Exception error)
        {
            id = "threw:" + error.GetType().Name;
        }

        Witness.Record(WitnessProtocol.SessionEnded + id
            + ";context=" + (HttpContext.Current == null ? "null" : "present"));
    }

</script>
