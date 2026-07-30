using System;
using System.Web.UI;

// Nothing here appends to PageProbe.Stages: the list is process-wide, so a request that mutated it
// would render a different body on the second request and the reuse assertion could not compare
// against the same expected output.
public partial class DefaultPage : Page
{
    protected string Stages
    {
        get { return string.Join("|", PageProbe.Stages.ToArray()); }
    }

    protected void Page_Load(object sender, EventArgs e)
    {
        Value.Text = Server.HtmlEncode(Request.QueryString["value"] ?? string.Empty);

        Items.DataSource = PageProbe.Items;
        Items.DataBind();
    }
}
