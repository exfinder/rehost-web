using System.Collections.Generic;
using System.Security.Principal;

namespace System.Security.Claims;

internal sealed class DeferredRoleClaimsIdentity : ClaimsIdentity
{
    private readonly List<IEnumerable<Claim>> deferred = new List<IEnumerable<Claim>>();

    internal DeferredRoleClaimsIdentity(IIdentity identity)
        : base(identity)
    {
    }

    // Framework's Clone drops ClaimsIdentity.ExternalClaims, so a copy carries no deferred claims.
    internal DeferredRoleClaimsIdentity(ClaimsIdentity other)
        : base(other)
    {
    }

    // ClaimsIdentity.ExternalClaims takes List<Claim> here, not Framework's
    // IEnumerable<Claim>, so a deferred sequence cannot be parked there.
    public override IEnumerable<Claim> Claims
    {
        get
        {
            foreach (var claim in base.Claims)
            {
                yield return claim;
            }

            foreach (var source in deferred)
            {
                foreach (var claim in source)
                {
                    yield return claim;
                }
            }
        }
    }

    internal void AddDeferredClaims(IEnumerable<Claim> claims) => deferred.Add(claims);

    public override ClaimsIdentity Clone() => new DeferredRoleClaimsIdentity(this);
}
