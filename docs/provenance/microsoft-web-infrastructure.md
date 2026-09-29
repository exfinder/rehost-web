# Microsoft.Web.Infrastructure

`Rehost.Web.Infrastructure` carries the public API of the
`Microsoft.Web.Infrastructure` 2.0.0 package. Nothing is imported. The package
ships under the Microsoft .NET Library EULA, which forbids decompiling it, so no
code from it and no decompiled output informs the port.

The API comes from the 2.0.0 assembly's metadata, confirmed by compiling a
`net48` project against the package. It is four public static classes and
seven methods, in the original namespaces `Microsoft.Web.Infrastructure`,
`Microsoft.Web.Infrastructure.DynamicModuleHelper` and
`Microsoft.Web.Infrastructure.DynamicValidationHelper`. The assembly carries the
package family version and ships inside `Rehost.Web`
([ADR 0009](../adr/0009-assembly-graph.md)).

Every method is new code that calls what the port's `System.Web` already has:

- `DynamicModuleUtility.RegisterModule` calls the public
  `HttpApplication.RegisterModule`.
- The three `ValidationUtility` methods call the runtime's internal
  `DynamicValidationShim`. The MIT reference source
  (`src/System.Web.ReferenceSource/DynamicValidationShim.cs`) says the class
  exists for `Microsoft.Web.Infrastructure` to call; the runtime grants access
  with one `InternalsVisibleTo`.
- `InfrastructureHelper.UnloadAppDomain` calls `HttpRuntime.UnloadAppDomain`.
- `InfrastructureHelper.IsCodeDomDefinedExtension` calls
  `CodeDomProvider.IsDefinedExtension`, as the port's own compilation code does.
- `HttpContextHelper.ExecuteInNullContext` sets `HttpContext.Current` to `null`
  for the action and restores it afterwards, also when the action throws.

None of the methods checks its arguments. Every `null` or wrong-type argument
the readings tried already fails with the observed exception inside the call.
The behavior these calls match is in
[readings MWI1-MWI5](../research/mwi-readings.md), taken against the shipped
2.0.0 assembly on IIS Express.
