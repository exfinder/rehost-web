using System;
using System.EnterpriseServices;

namespace System.Web.Util;

public delegate void TransactedCallback();

internal delegate int TransactedExecCallback();

public class Transactions
{
    private const string Message = "COM+ page transactions are not supported by Rehost.WebForms.";

    public static void InvokeTransacted(TransactedCallback callback, TransactionOption mode) =>
        throw new PlatformNotSupportedException(Message);

    public static void InvokeTransacted(
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
