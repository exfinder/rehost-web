<%@ Application Language="C#" %>
<script runat="server">

    void Application_EndRequest(object sender, EventArgs e)
    {
        try { Response.AppendHeader("X-After-End", "stamped"); }
        catch (Exception) { }

        if (Request.QueryString["fae"] != null)
        {
            try { Response.Flush(); } catch (Exception) { }
            try { Response.AppendHeader("X-Late-2", "yes"); }
            catch (Exception) { }
        }
    }

</script>
