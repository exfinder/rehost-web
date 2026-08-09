using System.Web.ModelBinding;

namespace Microsoft.AspNet.FriendlyUrls.ModelBinding;

public class FriendlyUrlSegmentsValueProvider : SimpleValueProvider
{
    private readonly int _index;

    public FriendlyUrlSegmentsValueProvider(
        ModelBindingExecutionContext modelBindingExecutionContext,
        int index)
        : base(modelBindingExecutionContext)
    {
        _index = index;
    }

    protected override object? FetchValue(string key)
    {
        var segments = ModelBindingExecutionContext.HttpContext.Request
            .GetFriendlyUrlSegments();
        return _index >= 0 && _index < segments.Count
            ? segments[_index]
            : null;
    }
}
