<%@ Application Language="C#" %>
<%@ Import Namespace="System.Threading" %>
<%@ Import Namespace="System.Threading.Tasks" %>
<script runat="server">

    public override void Init()
    {
        base.Init();
        var helper = new EventHandlerTaskAsyncHelper(OnBeginRequestAsync);
        AddOnBeginRequestAsync(helper.BeginEventHandler, helper.EndEventHandler);
    }

    private async Task OnBeginRequestAsync(object sender, EventArgs e)
    {
        var application = (HttpApplication)sender;
        var context = application.Context;
        var threadBefore = Environment.CurrentManagedThreadId;
        await Task.Delay(60);
        var current = HttpContext.Current;
        context.Items["module"] = "tid="
            + (Environment.CurrentManagedThreadId == threadBefore ? "before" : "other")
            + ";sc=" + (SynchronizationContext.Current == null ? "null" : SynchronizationContext.Current.GetType().Name)
            + ";ctx=" + (current == null ? "null" : ReferenceEquals(current, context) ? "same" : "other");
    }

</script>
