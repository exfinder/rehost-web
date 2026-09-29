using System;

namespace System.EnterpriseServices;

internal static class ContextUtil
{
    private const string Message = "COM+ transactions are not supported by Rehost.Web.";

    internal static bool IsInTransaction => throw new PlatformNotSupportedException(Message);

    internal static TransactionVote MyTransactionVote => throw new PlatformNotSupportedException(Message);
}
