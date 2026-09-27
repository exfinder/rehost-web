using System.Collections.Specialized;
using System.Web;

namespace Microsoft.Web.Infrastructure.DynamicValidationHelper;

public static class ValidationUtility
{
    public static void EnableDynamicValidation(HttpContext context) =>
        DynamicValidationShim.EnableDynamicValidation(context);

    public static bool? IsValidationEnabled(HttpContext context) =>
        DynamicValidationShim.IsValidationEnabled(context);

    public static void GetUnvalidatedCollections(
        HttpContext context,
        out Func<NameValueCollection> formGetter,
        out Func<NameValueCollection> queryStringGetter) =>
        DynamicValidationShim.GetUnvalidatedCollections(
            context, out formGetter, out queryStringGetter);
}
