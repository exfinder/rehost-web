using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Rehost.Web.ScenarioProbes;

// The page renders these into its own body, so event ordering needs no side channel.
public static class PostbackTrace
{
    private const string Key = "postback.trace";

    public static void Record(HttpContext context, string mark)
    {
        if (context == null)
        {
            return;
        }

        var marks = context.Items[Key] as List<string>;
        if (marks == null)
        {
            marks = new List<string>();
            context.Items[Key] = marks;
        }

        marks.Add(mark);
    }

    public static string Render(HttpContext context)
    {
        var marks = context?.Items[Key] as List<string>;
        return marks == null ? "" : string.Join(">", marks.ToArray());
    }
}

// Clicks rides on control state, Note on view state. Declared with EnableViewState="false" one
// survives a postback and the other does not, so no implementation can conflate the two.
public sealed class Counter : WebControl
{
    private int _clicks;

    public Counter()
        : base(HtmlTextWriterTag.Span)
    {
    }

    public int Clicks
    {
        get { return _clicks; }
    }

    public string Note
    {
        get
        {
            var note = ViewState["Note"] as string;
            return note ?? "";
        }
        set { ViewState["Note"] = value; }
    }

    public void Increment()
    {
        _clicks++;
    }

    protected override void OnInit(EventArgs e)
    {
        base.OnInit(e);
        Page.RegisterRequiresControlState(this);
    }

    protected override object SaveControlState()
    {
        PostbackTrace.Record(Context, "counter.save-control-state");
        return new Pair(base.SaveControlState(), _clicks);
    }

    protected override void LoadControlState(object savedState)
    {
        PostbackTrace.Record(Context, "counter.load-control-state");

        var pair = savedState as Pair;
        if (pair == null)
        {
            base.LoadControlState(savedState);
            return;
        }

        base.LoadControlState(pair.First);
        _clicks = (int)pair.Second;
    }

    protected override void RenderContents(HtmlTextWriter writer)
    {
        writer.Write("clicks=");
        writer.Write(_clicks.ToString(CultureInfo.InvariantCulture));
        writer.Write(" note=");
        writer.WriteEncodedText(Note);
    }
}
