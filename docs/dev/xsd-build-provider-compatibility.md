# XSD build-provider compatibility

App_Code typed `DataSet` generation from `.xsd` files is unsupported. Ordinary
XML schema APIs are unaffected.

Imported `Compilation/XsdBuildProvider.cs` remains preserved but excluded.
The internal replacement keeps configuration type resolution and throws
`PlatformNotSupportedException` from code generation.

The Framework path depends on the substantially larger
`System.Data.Design.TypedDataSetGenerator` stack. Restore it only if App_Code
typed DataSets enter the supported profile.

Implementation:
`src/Rehost.Web/Compatibility/Compilation/XsdBuildProvider.cs`.
