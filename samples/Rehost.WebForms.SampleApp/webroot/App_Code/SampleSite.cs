using System;
using System.Collections.Generic;
using System.Web;

namespace Sample
{
    public sealed class FeaturePage
    {
        public FeaturePage(string title, string url, string blurb)
        {
            Title = title;
            Url = url;
            Blurb = blurb;
        }

        public string Title { get; }

        public string Url { get; }

        public string Blurb { get; }
    }

    public static class FeatureCatalog
    {
        public static readonly IList<FeaturePage> Pages = new List<FeaturePage>
        {
            new FeaturePage("Postback", "~/Postback.aspx",
                "View state, control state, and event routing: three counters that survive differently."),
            new FeaturePage("Cookies", "~/Cookies.aspx",
                "Set, read, and delete cookies; watch the raw Set-Cookie semantics."),
            new FeaturePage("Upload", "~/Upload.aspx",
                "Multipart upload through FileUpload, saved with HttpPostedFile.SaveAs."),
            new FeaturePage("Termination", "~/Termination.aspx",
                "Response.End, Response.Redirect, and CompleteRequest: which bytes survive."),
            new FeaturePage("Validation", "~/Validation.aspx",
                "Request validation refusing dangerous input before the page runs."),
            new FeaturePage("Resources", "~/Resources.aspx",
                "App_GlobalResources through the generated class, with a culture switch."),
        };
    }

    public static class SampleTrace
    {
        private const string Key = "sample:trace";

        public static void Record(HttpContext context, string entry)
        {
            if (context == null)
            {
                return;
            }

            var entries = context.Items[Key] as List<string>;
            if (entries == null)
            {
                entries = new List<string>();
                context.Items[Key] = entries;
            }

            entries.Add(entry);
        }

        public static string Render(HttpContext context)
        {
            var entries = context == null ? null : context.Items[Key] as List<string>;
            if (entries == null || entries.Count == 0)
            {
                return "(no trace)";
            }

            return System.Net.WebUtility.HtmlEncode(string.Join("  →  ", entries));
        }
    }
}

namespace Sample.Controls
{
    using System.Web.UI;

    // Clicks ride control state, which EnableViewState="false" cannot turn off; the page
    // demonstrates the difference against a view-state Label and a per-request field.
    public class ClickCounter : Control
    {
        private int _clicks;

        public int Clicks => _clicks;

        public void Increment() => _clicks++;

        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);
            Page.RegisterRequiresControlState(this);
        }

        protected override object SaveControlState() => _clicks;

        protected override void LoadControlState(object savedState)
        {
            _clicks = savedState is int clicks ? clicks : 0;
        }

        protected override void Render(HtmlTextWriter writer)
        {
            writer.Write(_clicks);
        }
    }
}
