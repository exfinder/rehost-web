using System.Configuration;
using System.Web.Hosting;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Hosting;

// <cache privateBytesLimit> bounds the cache's own object graph, which only System.SizedReference
// could measure. Accepting it silently would leave an application believing it had a bound.
public sealed class CacheSizeLimitTests
{
    [Fact]
    public void A_Configured_Cache_Size_Limit_Is_Refused()
    {
        var refusal = Should.Throw<ConfigurationErrorsException>(
            () => UnsupportedCacheOptions.RejectPrivateBytesLimit(209715200));

        refusal.Message.ShouldContain("privateBytesLimit", Case.Sensitive);
        refusal.Message.ShouldContain("processModel memoryLimit", Case.Sensitive);
    }

    [Fact]
    public void The_Unset_Default_Is_Accepted()
    {
        Should.NotThrow(() => UnsupportedCacheOptions.RejectPrivateBytesLimit(0));
    }
}
