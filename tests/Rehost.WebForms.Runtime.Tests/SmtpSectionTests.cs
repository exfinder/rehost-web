using System.Net.Configuration;
using System.Net.Mail;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests;

public sealed class SmtpSectionTests
{
    [Fact]
    public void Defaults_and_public_configuration_values_match_framework()
    {
        var section = new SmtpSection();

        section.DeliveryMethod.ShouldBe(SmtpDeliveryMethod.Network);
        section.DeliveryFormat.ShouldBe(SmtpDeliveryFormat.SevenBit);
        section.Network.Port.ShouldBe(25);
        section.Network.DefaultCredentials.ShouldBeFalse();
        section.Network.EnableSsl.ShouldBeFalse();

        section.From = "sender@example.test";
        section.Network.Host = "smtp.example.test";
        section.Network.Port = 2525;
        section.Network.EnableSsl = true;

        section.From.ShouldBe("sender@example.test");
        section.Network.Host.ShouldBe("smtp.example.test");
        section.Network.Port.ShouldBe(2525);
        section.Network.EnableSsl.ShouldBeTrue();
    }
}
