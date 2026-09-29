using System.Security;
using System.Threading;

namespace System;

internal class AppDomainManager
{
    public virtual HostExecutionContextManager HostExecutionContextManager => null;

    public virtual HostSecurityManager HostSecurityManager => null;
}
