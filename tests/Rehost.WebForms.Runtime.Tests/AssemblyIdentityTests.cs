using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests;

public sealed class AssemblyIdentityTests
{
    [Fact]
    public void RuntimeOwnsSystemWebCompatibilityTypes()
    {
        var runtimeAssembly = typeof(System.Web.HttpUtility).Assembly;

        runtimeAssembly.GetName().Name.ShouldBe("Rehost.WebForms.Runtime");
        typeof(System.Web.IHtmlString).Assembly.ShouldBe(runtimeAssembly);
        typeof(System.Web.Security.MembershipUser).Assembly.GetName().Name
            .ShouldBe("Rehost.WebForms.ApplicationServices");
    }
}
