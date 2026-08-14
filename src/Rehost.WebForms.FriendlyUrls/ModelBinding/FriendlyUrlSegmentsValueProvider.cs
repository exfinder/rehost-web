using System;
using System.Globalization;
using System.Web.ModelBinding;

namespace Microsoft.AspNet.FriendlyUrls.ModelBinding;

public class FriendlyUrlSegmentsValueProvider : SimpleValueProvider
{
    private readonly int _index;

    public FriendlyUrlSegmentsValueProvider(
        ModelBindingExecutionContext modelBindingExecutionContext,
        int index)
        : base(modelBindingExecutionContext, CultureInfo.InvariantCulture)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        _index = index;
    }

    protected override object? FetchValue(string key)
    {
        var segments = ModelBindingExecutionContext.HttpContext.Request
            .GetFriendlyUrlSegments();
        return segments == null || segments.Count <= _index
            ? null
            : segments[_index];
    }
}
