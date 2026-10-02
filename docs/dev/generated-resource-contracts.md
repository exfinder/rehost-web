# Generated resource contracts

System.Web restores two internal build/runtime contracts:

- `SYSTEM_WEB`, selecting the original shared `LocalizableString.cs` branch;
- `System.Resources.Tools.StronglyTypedResourceBuilder`, adapted from
  dotnet/msbuild; identity and license are in [sources](sources.md).

The builder is checked-in source, not a generator. It retains only the
`IDictionary` path used by `BaseResourcesBuildProvider`, Framework identifier
cleanup/collision rules, ordering, resource-manager identity, and CodeDOM
shape. Unused `ResXDataNode` overloads and
`ITargetAwareCodeDomProvider` behavior are omitted.

The type remains internal and cross-platform. Existing
`System.Web.resources` deterministic generation is unchanged.

Implementation:
`src/Rehost.Web/Compatibility/Resources/StronglyTypedResourceBuilder.cs`.
Remaining oracle/provider work:
[generated resources](follow-ups/generated-resource-compatibility.md).
