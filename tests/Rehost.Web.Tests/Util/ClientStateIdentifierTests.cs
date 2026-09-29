using System.Web.Util;
using Shouldly;
using Xunit;

namespace Rehost.Web.Tests.Util;

// PostbackOverKestrelTests pins the rendered __VIEWSTATEGENERATOR to 72DAA2F9, but that constant
// came from this port's own output, so on its own it proves stability rather than correctness.
// This recomputes it from the two inputs Page.GetClientStateIdentifier combines, which is the
// claim ledger P48 actually makes.
public sealed class ClientStateIdentifierTests
{
    [Fact]
    public void The_Generator_Value_Is_The_Stable_Hash_Of_The_Directory_And_Type_Name()
    {
        var pageHashCode =
            StringUtil.GetNonRandomizedHashCode("/", ignoreCase: true)
            + StringUtil.GetNonRandomizedHashCode("default_aspx", ignoreCase: true);

        ((uint)pageHashCode).ShouldBe(0x72DAA2F9u);
    }

    // Framework reaches string.GetHashCode and StringComparer.InvariantCultureIgnoreCase here,
    // which .NET randomizes per process. Equal hashes across two calls in one process would hold
    // either way; what distinguishes the stable algorithm is that the value is a fixed constant.
    [Fact]
    public void The_Hash_Does_Not_Depend_On_Per_Process_Randomization()
    {
        StringUtil.GetNonRandomizedHashCode("default_aspx", ignoreCase: true)
            .ShouldNotBe("default_aspx".GetHashCode());
    }
}
