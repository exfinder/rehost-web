using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Web;

namespace Microsoft.AspNet.FriendlyUrls.Resolvers;

public class FriendlyUrlResolver : IFriendlyUrlResolver
{
    private readonly string _fileExtension;
    private readonly IList<string> _fileExtensions;

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
        _fileExtensions = new ReadOnlyCollection<string>([_fileExtension]);
    }

    public virtual string? ConvertToFriendlyUrl(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        if (string.Equals(
                VirtualPathUtility.GetExtension(path),
                _fileExtension,
                StringComparison.OrdinalIgnoreCase))
        {
            return path.Remove(path.LastIndexOf(_fileExtension, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    public virtual IList<string> GetExtensions(HttpContextBase? httpContext)
    {
        return _fileExtensions;
    }

    public virtual void PreprocessRequest(
        HttpContextBase httpContext,
        IHttpHandler httpHandler)
    {
    }
}
