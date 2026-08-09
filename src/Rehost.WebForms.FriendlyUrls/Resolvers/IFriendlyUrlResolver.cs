using System.Collections.Generic;
using System.Web;

namespace Microsoft.AspNet.FriendlyUrls.Resolvers;

public interface IFriendlyUrlResolver
{
    string? ConvertToFriendlyUrl(string? path);

    IList<string> GetExtensions(HttpContextBase? httpContext);

    void PreprocessRequest(HttpContextBase httpContext, IHttpHandler httpHandler);
}
