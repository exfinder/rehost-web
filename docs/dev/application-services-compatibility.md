# Application Services compatibility

`Rehost.Web.ApplicationServices` preserves the Reference Source
membership/application-services sibling boundary. Its pinned Microsoft sources
remain authoritative.

Modern .NET cannot create or unload the custom loader's secondary AppDomain.
The project-owned `Compatibility/CustomLoaderHelper.cs` therefore creates the
loader in-process. This supplies no isolation, unload, remoting, or security
boundary and follows the one-application-per-process contract.

The Runtime consumes the original internal membership adapter through a
friend-assembly relationship. Shared assembly identity constants come from the
compile-time-only BuildInputs project; it is not a product dependency.

Current assembly:
`src/Rehost.Web.ApplicationServices/Rehost.Web.ApplicationServices.csproj`.
Loader adaptation:
`src/Rehost.Web.ApplicationServices/Compatibility/CustomLoaderHelper.cs`.
