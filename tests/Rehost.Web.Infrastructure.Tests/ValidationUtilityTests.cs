using Microsoft.Web.Infrastructure.DynamicValidationHelper;
using Shouldly;
using Xunit;

namespace Rehost.Web.Infrastructure.Tests;

public sealed class ValidationUtilityTests
{
    [Fact]
    public void Enable_Dynamic_Validation_Refuses_A_Null_Context() =>
        Should.Throw<NullReferenceException>(
            () => ValidationUtility.EnableDynamicValidation(null!));

    [Fact]
    public void Is_Validation_Enabled_Refuses_A_Null_Context() =>
        Should.Throw<NullReferenceException>(() => ValidationUtility.IsValidationEnabled(null!));

    [Fact]
    public void Get_Unvalidated_Collections_Refuses_A_Null_Context() =>
        Should.Throw<NullReferenceException>(
            () => ValidationUtility.GetUnvalidatedCollections(null!, out _, out _));
}
