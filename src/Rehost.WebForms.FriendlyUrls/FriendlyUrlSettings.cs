using System;

namespace Microsoft.AspNet.FriendlyUrls;

public sealed class FriendlyUrlSettings
{
    private RedirectMode _autoRedirectMode = RedirectMode.Off;
    private ResolverCachingMode _resolverCachingMode = ResolverCachingMode.Static;

    public string SwitchViewRouteName { get; set; } = "AspNet.FriendlyUrls.SwitchView";

    public string SwitchViewUrl { get; set; } = "__FriendlyUrls_SwitchView/{view}";

    public RedirectMode AutoRedirectMode
    {
        get => _autoRedirectMode;
        set
        {
            if (value < RedirectMode.Permanent || value > RedirectMode.Off)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            _autoRedirectMode = value;
        }
    }

    public ResolverCachingMode ResolverCachingMode
    {
        get => _resolverCachingMode;
        set
        {
            if (value < ResolverCachingMode.Static || value > ResolverCachingMode.Disabled)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            _resolverCachingMode = value;
        }
    }
}
