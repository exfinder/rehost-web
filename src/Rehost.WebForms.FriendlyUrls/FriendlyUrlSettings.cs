namespace Microsoft.AspNet.FriendlyUrls;

public sealed class FriendlyUrlSettings
{
    public string SwitchViewRouteName { get; set; } = "AspNet.FriendlyUrls.SwitchView";

    public string SwitchViewUrl { get; set; } = "__FriendlyUrls_SwitchView/{view}";

    public RedirectMode AutoRedirectMode { get; set; } = RedirectMode.Off;

    public ResolverCachingMode ResolverCachingMode { get; set; } = ResolverCachingMode.Static;
}
