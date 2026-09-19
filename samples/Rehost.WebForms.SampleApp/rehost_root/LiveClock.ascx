<%@ Control Language="C#" %>
<script runat="server">

    protected void Page_Load(object sender, EventArgs e)
    {
        Sample.SampleTrace.Record(Context, "LiveClock.Page_Load");
        Stamp.Text = DateTime.Now.ToString("HH:mm:ss.fff");
    }

</script>
<div class="panel clock">
    <strong>Live control</strong>
    <span>rendered at <asp:Label ID="Stamp" runat="server" /></span>
    <small>my Label's ClientID: <code><%= Stamp.ClientID %></code></small>
</div>
