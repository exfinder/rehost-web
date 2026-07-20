# XSD build-provider compatibility

## Decision

App_Code typed `DataSet` generation from `.xsd` files is explicitly unsupported.
Ordinary XML schema APIs are unaffected.

The imported `Compilation/XsdBuildProvider.cs` remains untouched but is excluded
from compilation. Its project-owned internal replacement preserves configuration
type resolution and throws `PlatformNotSupportedException` from `GenerateCode`.

## Provenance and deviation

The pinned Microsoft System.Web Reference Source delegates generation to
`System.Data.Design.TypedDataSetGenerator` from Framework `System.Design`. The
local Microsoft Reference Source clone contains older generator machinery, but
porting that design-time stack is materially larger than the current runtime
scope. The POC also excluded this provider.

This intentionally removes Framework typed-DataSet generation behavior. Revisit
only when App_Code typed DataSets become a supported requirement.

## Validation

xUnit v3 + Shouldly verifies the explicit failure. Both XSD diagnostics are
removed. The mandated Runtime build advances to a newly exposed cascade of 79
errors/2,441 warnings; this change does not address those later groups. Imported
Reference Source remains unchanged.
