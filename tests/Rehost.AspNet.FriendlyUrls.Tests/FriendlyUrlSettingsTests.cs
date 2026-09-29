using Microsoft.AspNet.FriendlyUrls;
using Shouldly;
using Xunit;

namespace Rehost.AspNet.FriendlyUrls.Tests;

public sealed class FriendlyUrlSettingsTests
{
    [Fact]
    public void NewSettingsUseFrameworkDefaults()
    {
        var settings = new FriendlyUrlSettings();

        settings.SwitchViewRouteName.ShouldBe("AspNet.FriendlyUrls.SwitchView");
        settings.SwitchViewUrl.ShouldBe("__FriendlyUrls_SwitchView/{view}");
        settings.AutoRedirectMode.ShouldBe(RedirectMode.Off);
        settings.ResolverCachingMode.ShouldBe(ResolverCachingMode.Static);
    }

    [Fact]
    public void OutOfRangeValuesAreRejected()
    {
        var settings = new FriendlyUrlSettings();

        Should.Throw<ArgumentOutOfRangeException>(
            () => settings.AutoRedirectMode = (RedirectMode)3);
        Should.Throw<ArgumentOutOfRangeException>(
            () => settings.ResolverCachingMode = (ResolverCachingMode)(-1));
    }
}
