using Rehost.Web.AspNetCore;
using Shouldly;
using Xunit;

namespace Rehost.Web.AspNetCore.Tests;

// The three byte shapes of reading R5 and what IIS handed Framework for each.
public sealed class RequestHeaderEncodingTests
{
    [Fact]
    public void Valid_Utf8_Decodes_As_Utf8()
    {
        RequestHeaderEncoding.Instance.GetString("café 中"u8.ToArray()).ShouldBe("café 中");
    }

    [Fact]
    public void A_Latin1_Byte_Decodes_As_Latin1()
    {
        RequestHeaderEncoding.Instance.GetString([0x63, 0x61, 0x66, 0xE9]).ShouldBe("café");
    }

    [Fact]
    public void Invalid_Utf8_Decodes_As_Latin1_Instead_Of_Failing()
    {
        RequestHeaderEncoding.Instance.GetString([0x61, 0xFF, 0xFE, 0x62]).ShouldBe("aÿþb");
    }
}
