<%@ Page Language="C#" %>
<%@ Import Namespace="Rehost.WebForms.ScenarioProbes" %>
<script runat="server">

    void Page_Load(object sender, EventArgs e)
    {
        Response.Write("before|");
        try
        {
            Response.End();
        }
        catch (Exception ex)
        {
            WitnessJournal.Stage(Request, "swallowed:" + ex.GetType().Name);
            Response.Write("swallowed-write|");
            try { Response.End(); }
            catch (Exception) { }
            WitnessJournal.Stage(Request, "after-second-end");
        }
        Response.Write("tail|");
    }

</script>
