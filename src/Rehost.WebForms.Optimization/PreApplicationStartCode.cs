using System.ComponentModel;

namespace System.Web.Optimization;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class PreApplicationStartCode
{
    private static bool _startWasCalled;

    public static void Start()
    {
        if (_startWasCalled)
        {
            return;
        }

        _startWasCalled = true;
        HttpApplication.RegisterModule(typeof(BundleModule));
    }
}
