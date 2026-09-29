using System.ComponentModel;
using System.Web;

namespace Microsoft.AspNet.FriendlyUrls;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class PreApplicationStartCode
{
    public static void Start()
    {
        HttpApplication.RegisterModule(typeof(FriendlyUrlsModule));
    }
}
