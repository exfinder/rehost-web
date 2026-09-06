using System.Security.Principal;

public static class WindowsIdentityImpersonationExtensions
{
    public static WindowsImpersonationContext Impersonate(this WindowsIdentity identity) =>
        throw new PlatformNotSupportedException(
            "Thread impersonation has no portable implementation; run the application under the identity its background work requires.");
}
