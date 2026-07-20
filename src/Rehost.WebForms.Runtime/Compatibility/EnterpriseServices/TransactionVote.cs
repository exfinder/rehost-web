using System;

namespace System.EnterpriseServices;

[Serializable]
internal enum TransactionVote
{
    Commit = 0,
    Abort = 1
}
