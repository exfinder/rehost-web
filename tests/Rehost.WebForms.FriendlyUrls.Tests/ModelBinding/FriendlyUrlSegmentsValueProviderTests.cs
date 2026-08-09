using Microsoft.AspNet.FriendlyUrls.ModelBinding;
using Shouldly;
using System.Web;
using System.Web.ModelBinding;
using System.Web.Routing;
using Xunit;

namespace Rehost.WebForms.FriendlyUrls.Tests.ModelBinding;

public sealed class FriendlyUrlSegmentsValueProviderTests
{
    [Fact]
    public void AttributeSelectsRequestedFriendlyUrlSegment()
    {
        var routeData = new RouteData();
        routeData.DataTokens["FriendlyUrlSegments"] = new List<string> { "catalog", "42" };
        var context = new TestHttpContext(routeData);
        var executionContext = new ModelBindingExecutionContext(
            context,
            new ModelStateDictionary());
        var attribute = new FriendlyUrlSegmentsAttribute(1);

        attribute.Index.ShouldBe(1);

        var result = attribute.GetValueProvider(executionContext).GetValue("id");

        result.ShouldNotBeNull();
        result.RawValue.ShouldBe("42");
        result.AttemptedValue.ShouldBe("42");
    }

    [Fact]
    public void MissingSegmentProducesNoValue()
    {
        var routeData = new RouteData();
        routeData.DataTokens["FriendlyUrlSegments"] = new List<string> { "catalog" };
        var context = new TestHttpContext(routeData);
        var executionContext = new ModelBindingExecutionContext(
            context,
            new ModelStateDictionary());
        var provider = new FriendlyUrlSegmentsValueProvider(executionContext, 2);

        provider.GetValue("id").ShouldBeNull();
    }

    private sealed class TestHttpContext : HttpContextBase
    {
        private readonly HttpRequestBase _request;

        internal TestHttpContext(RouteData routeData)
        {
            _request = new TestHttpRequest(new RequestContext(this, routeData));
        }

        public override HttpRequestBase Request => _request;
    }

    private sealed class TestHttpRequest(RequestContext requestContext) : HttpRequestBase
    {
        public override RequestContext RequestContext { get; set; } = requestContext;
    }
}
