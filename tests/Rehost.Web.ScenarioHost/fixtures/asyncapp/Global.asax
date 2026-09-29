<%@ Application Language="C#" %>
<%@ Import Namespace="System.Threading" %>
<%@ Import Namespace="Rehost.Web.ScenarioProbes" %>
<%@ Import Namespace="System.Threading.Tasks" %>
<script runat="server">

    public override void Init()
    {
        base.Init();
        var helper = new EventHandlerTaskAsyncHelper(OnBeginRequestAsync);
        AddOnBeginRequestAsync(helper.BeginEventHandler, helper.EndEventHandler);
        var map = new EventHandlerTaskAsyncHelper(OnMapRequestHandlerAsync);
        AddOnMapRequestHandlerAsync(map.BeginEventHandler, map.EndEventHandler);
        var log = new EventHandlerTaskAsyncHelper(OnLogRequestAsync);
        AddOnLogRequestAsync(log.BeginEventHandler, log.EndEventHandler);
        var postLog = new EventHandlerTaskAsyncHelper(OnPostLogRequestAsync);
        AddOnPostLogRequestAsync(postLog.BeginEventHandler, postLog.EndEventHandler);
    }

    private async Task OnLogRequestAsync(object sender, EventArgs e)
    {
        await Task.Delay(1);
        Witness.Stage(((HttpApplication)sender).Context.Request, "AsyncLogRequest");
    }

    private async Task OnMapRequestHandlerAsync(object sender, EventArgs e)
    {
        await Task.Delay(1);
        Witness.Stage(((HttpApplication)sender).Context.Request, "AsyncMapRequestHandler");
    }

    private async Task OnPostLogRequestAsync(object sender, EventArgs e)
    {
        await Task.Delay(1);
        Witness.Stage(((HttpApplication)sender).Context.Request, "AsyncPostLogRequest");
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
