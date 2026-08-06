<%@ Page Language="C#" MasterPageFile="~/Site.master" %>
<script runat="server">

    protected void Page_Load(object sender, EventArgs e)
    {
        Sample.SampleTrace.Record(Context, "Termination.Page_Load");

        switch (Request.QueryString["mode"])
        {
            case "end":
                Response.Write("[written before Response.End — this survives]");
                Response.End();
                Response.Write("[written after Response.End — never sent, never runs]");
                break;

            case "redirect":
                Response.Write("[buffered before Redirect — replaced by the 302 body]");
                Response.Redirect("Termination.aspx");
                break;

            case "complete":
                Note.Text = "CompleteRequest() was called in Page_Load: no unwind, this page "
                    + "still rendered to the buffer, and the remaining pipeline stages were "
                    + "skipped. EndRequest still ran — after rendering, so it can never show in "
                    + "the footer trace; see the <code>X-Sample-EndRequest</code> response "
                    + "header in your browser's dev tools.";
                Context.ApplicationInstance.CompleteRequest();
                break;
        }
    }

</script>
<asp:Content ContentPlaceHolderID="Main" runat="server">

<h1>Request termination</h1>
<p class="lede">
    Framework ended requests with a thread abort; this runtime preserves the observable
    contract without one. Each link below terminates this very request a different way.
</p>

<div class="cards">
    <a class="card" href="Termination.aspx?mode=end">
        <h2>Response.End</h2>
        <p>Writes a line, calls <code>End()</code>. Bytes before the call arrive, code after it
        never runs, and the page body you are reading now is never rendered — the response is
        just that one line. The <code>X-Sample-EndRequest</code> header still arrives:
        headers stamped in <code>EndRequest</code> after <code>End</code> reach the wire,
        as measured on Framework's abort arm (ledger P55). An application's own
        <code>Flush()</code> is what seals them.</p>
    </a>
    <a class="card" href="Termination.aspx?mode=redirect">
        <h2>Response.Redirect</h2>
        <p>Buffered output is replaced by Framework's exact
        "Object moved" body and a 302 back here — the browser lands on this page again.</p>
    </a>
    <a class="card" href="Termination.aspx?mode=complete">
        <h2>CompleteRequest</h2>
        <p>No unwind at all: the page finishes rendering, later pipeline stages are skipped,
        <code>EndRequest</code> still runs.</p>
    </a>
</div>

<p><asp:Label ID="Note" runat="server" /></p>

</asp:Content>
