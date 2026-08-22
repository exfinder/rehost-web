using System.Collections.Generic;
using System.ComponentModel;

namespace System.Security.Claims;

public static class DynamicRoleClaimProvider
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("Use ClaimsAuthenticationManager to add claims to a ClaimsIdentity", true)]
    public static void AddDynamicRoleClaims(ClaimsIdentity claimsIdentity, IEnumerable<Claim> claims)
    {
        if (claimsIdentity is DeferredRoleClaimsIdentity deferred)
        {
            deferred.AddDeferredClaims(claims);
        }
        else
        {
            claimsIdentity.AddClaims(claims);
        }
    }
}
