using System;
using System.Web.ModelBinding;

namespace Microsoft.AspNet.FriendlyUrls.ModelBinding;

[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class FriendlyUrlSegmentsAttribute : ValueProviderSourceAttribute
{
    public FriendlyUrlSegmentsAttribute(int index)
    {
        Index = index;
    }

    public int Index { get; }

    public override IValueProvider GetValueProvider(
        ModelBindingExecutionContext modelBindingExecutionContext)
    {
        ArgumentNullException.ThrowIfNull(modelBindingExecutionContext);
        return new FriendlyUrlSegmentsValueProvider(modelBindingExecutionContext, Index);
    }
}
