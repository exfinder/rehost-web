using System;

namespace System.EnterpriseServices;

[Serializable]
public enum TransactionOption
{
    Disabled = 0,
    NotSupported = 1,
    Supported = 2,
    Required = 3,
    RequiresNew = 4
}
