using System.Security;
using System.Security.Permissions;

#pragma warning disable SYSLIB0003 // Approved compile-only CAS contract; modern runtime does not enforce CAS.

namespace System.Data.Common;

internal static class DbProviderFactoryExtensions
{
    internal static CodeAccessPermission CreatePermission(
        this DbProviderFactory factory,
        PermissionState state) => null;
}

#pragma warning restore SYSLIB0003
