# Generated resource contracts

System.Web restores two internal build/runtime contracts:

- `SYSTEM_WEB`, selecting the original shared `LocalizableString.cs` branch;
- `System.Resources.Tools.StronglyTypedResourceBuilder`, adapted from
  `dotnet/msbuild` commit
  `39950c6284e1fe6e68890e3655ea7704b42e6a32`.

The builder is checked-in source, not a generator. It retains only the
`IDictionary` path used by `BaseResourcesBuildProvider`, Framework identifier
cleanup/collision rules, ordering, resource-manager identity, and CodeDOM
shape. Unused `ResXDataNode` overloads and
`ITargetAwareCodeDomProvider` behavior are omitted.

The type remains internal and cross-platform. Existing
`System.Web.resources` deterministic generation is unchanged.

Implementation:
`src/Rehost.WebForms.Runtime/Compatibility/Resources/StronglyTypedResourceBuilder.cs`.
Tests:
`tests/Rehost.WebForms.Runtime.Tests/StronglyTypedResourceBuilderTests.cs`.
Remaining oracle/provider work:
[generated resources](follow-ups/generated-resource-compatibility.md).
