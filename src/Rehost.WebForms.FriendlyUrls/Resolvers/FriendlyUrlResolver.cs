using System;
using System.Collections.Generic;
using System.Web;

namespace Microsoft.AspNet.FriendlyUrls.Resolvers;

public class FriendlyUrlResolver : IFriendlyUrlResolver
{
    private readonly string _fileExtension;

    public FriendlyUrlResolver(string fileExtension)
    {
        if (string.IsNullOrEmpty(fileExtension))
        {
            throw new ArgumentException("Argument must not be null or empty.", nameof(fileExtension));
        }

        if (fileExtension[0] != '.')
        {
            throw new ArgumentException(
                "The file extension must contain a leading period, e.g. '.ext'.",
                nameof(fileExtension));
        }

        _fileExtension = fileExtension;
    }

    public virtual string? ConvertToFriendlyUrl(string? path)
    {
        if (string.IsNullOrEmpty(path) ||
            !path.EndsWith(_fileExtension, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return VirtualPathUtility.ToAbsolute(path[..^_fileExtension.Length]);
    }

    public virtual IList<string> GetExtensions(HttpContextBase? httpContext)
    {
        return new[] { _fileExtension };
    }

    public virtual void PreprocessRequest(
        HttpContextBase httpContext,
        IHttpHandler httpHandler)
    {
    }
}
