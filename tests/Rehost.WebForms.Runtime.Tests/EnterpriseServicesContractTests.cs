using Shouldly;
using System.EnterpriseServices;
using System.Web.Util;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests;

public sealed class EnterpriseServicesContractTests
{
    [Fact]
    public void Transaction_enums_match_framework_contract()
    {
        typeof(TransactionOption).IsNotPublic.ShouldBeTrue();
        HasSerializableAttribute(typeof(TransactionOption)).ShouldBeTrue();
        Enum.GetNames<TransactionOption>().ShouldBe(
            new[] { "Disabled", "NotSupported", "Supported", "Required", "RequiresNew" });
        Enum.GetValues<TransactionOption>().Select(value => (int)value).ShouldBe(new[] { 0, 1, 2, 3, 4 });

        typeof(TransactionVote).IsNotPublic.ShouldBeTrue();
        HasSerializableAttribute(typeof(TransactionVote)).ShouldBeTrue();
        ((int)TransactionVote.Commit).ShouldBe(0);
        ((int)TransactionVote.Abort).ShouldBe(1);
    }

    [Fact]
    public void ContextUtil_does_not_silently_claim_transaction_support()
    {
        Should.Throw<PlatformNotSupportedException>(() => _ = ContextUtil.IsInTransaction);
        Should.Throw<PlatformNotSupportedException>(() => _ = ContextUtil.MyTransactionVote);
    }

    [Theory]
    [InlineData((int)TransactionOption.Disabled)]
    [InlineData((int)TransactionOption.Required)]
    public void InvokeTransacted_is_explicitly_unsupported_on_every_platform(int optionValue)
    {
        var option = (TransactionOption)optionValue;
        var invoked = false;
        var aborted = false;

        Should.Throw<PlatformNotSupportedException>(() =>
            Transactions.InvokeTransacted(() => invoked = true, option, ref aborted));

        invoked.ShouldBeFalse();
        aborted.ShouldBeFalse();
    }

    [Fact]
    public void Transaction_probes_fall_back_to_false_when_COM_plus_is_unavailable()
    {
        Transactions.Utils.IsInTransaction.ShouldBeFalse();
        Transactions.Utils.AbortPending.ShouldBeFalse();
    }

    private static bool HasSerializableAttribute(Type type) =>
        type.CustomAttributes.Any(attribute => attribute.AttributeType == typeof(SerializableAttribute));
}
