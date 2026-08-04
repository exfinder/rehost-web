using Shouldly;
using System.EnterpriseServices;
using System.Web.Util;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests;

public sealed class EnterpriseServicesContractTests
{
    [Fact]
    public void ContextUtil_Does_Not_Silently_Claim_Transaction_Support()
    {
        Should.Throw<PlatformNotSupportedException>(() => _ = ContextUtil.IsInTransaction);
        Should.Throw<PlatformNotSupportedException>(() => _ = ContextUtil.MyTransactionVote);
    }

    [Theory]
    [InlineData((int)TransactionOption.Disabled)]
    [InlineData((int)TransactionOption.Required)]
    public void InvokeTransacted_Is_Explicitly_Unsupported_On_Every_Platform(int optionValue)
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
    public void Transaction_Probes_Fall_Back_To_False_When_COM_Plus_Is_Unavailable()
    {
        Transactions.Utils.IsInTransaction.ShouldBeFalse();
        Transactions.Utils.AbortPending.ShouldBeFalse();
    }
}
