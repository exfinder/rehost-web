<%@ Page Language="C#" MasterPageFile="~/Site.master" %>
<%@ Register Src="~/LiveClock.ascx" TagPrefix="uc" TagName="LiveClock" %>
<%@ Register Src="~/CachedClock.ascx" TagPrefix="uc" TagName="CachedClock" %>
<script runat="server">

    protected void Page_Load(object sender, EventArgs e)
    {
        Sample.SampleTrace.Record(Context, "Controls.Page_Load");
    }

</script>
<asp:Content ContentPlaceHolderID="Main" runat="server">

<h1>User controls</h1>
<p class="lede">
    Two <code>.ascx</code> controls, registered with <code>&lt;%@ Register %&gt;</code> and
    composed into this page inside the <code>Site.master</code> chrome. The second one carries
    <code>&lt;%@ OutputCache Duration="15" %&gt;</code>, so ASP.NET replays its rendered
    fragment instead of running it: reload and watch the live stamp tick while the cached one
    holds still for fifteen seconds.
</p>

<uc:LiveClock ID="Live" runat="server" />
<uc:CachedClock ID="Cached" runat="server" />

<p>
    <asp:HyperLink runat="server" NavigateUrl="~/Controls.aspx">Reload this page</asp:HyperLink>
</p>
<p>
    The <code>ClientID</code> above shows the naming container at work: the control's inner
    <code>Label</code> renders with an ID prefixed by every container on the way down — the
    same mangling .NET Framework produced, so client script written against it keeps working.
</p>

</asp:Content>
