using System.Web;

namespace Rehost.Web.ScenarioProbes;

public sealed class RefusingEventModule : IHttpModule
{
    public event EventHandler Refuse
    {
        add => throw new InvalidOperationException("refused-on-purpose");
        remove { }
    }

    public void Init(HttpApplication application)
    {
    }

    public void Dispose()
    {
    }
}
