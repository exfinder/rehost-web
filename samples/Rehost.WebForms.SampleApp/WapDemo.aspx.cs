using System;
using System.Web.UI;

namespace Sample
{
    public partial class WapDemo : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                Answer.Text = "compiled ahead of time, waiting for a postback";
            }
        }

        protected void Greet_Click(object sender, EventArgs e)
        {
            Answer.Text = "Hello, " + Server.HtmlEncode(Name.Text) + " — from the bin assembly.";
        }
    }
}
