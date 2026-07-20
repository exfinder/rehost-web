using System;
using System.EnterpriseServices;

namespace System.Web.Util;

internal delegate void TransactedCallback();

internal delegate int TransactedExecCallback();

internal class Transactions
{
    private const string Message = "COM+ page transactions are not supported by Rehost.WebForms.";

    internal static void InvokeTransacted(TransactedCallback callback, TransactionOption mode) =>
        throw new PlatformNotSupportedException(Message);

    internal static void InvokeTransacted(
        TransactedCallback callback,
        TransactionOption mode,
        ref bool transactionAborted) =>
        throw new PlatformNotSupportedException(Message);

    internal class Utils
    {
        private Utils()
        {
        }

        internal static bool IsInTransaction
        {
            get
            {
                try
                {
                    return ContextUtil.IsInTransaction;
                }
                catch (PlatformNotSupportedException)
                {
                    return false;
                }
            }
        }

        internal static bool AbortPending
        {
            get
            {
                try
                {
                    return ContextUtil.MyTransactionVote == TransactionVote.Abort;
                }
                catch (PlatformNotSupportedException)
                {
                    return false;
                }
            }
        }
    }
}
