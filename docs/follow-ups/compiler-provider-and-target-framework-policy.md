# Compiler provider and target-framework policy

Status: partially resolved. Priority: medium. Depends on runtime codegen/loading.

The provider contract is decided and implemented; see
[ADR 0041](../adr/0041-compile-pages-through-a-configured-roslyn-provider.md).
What remains is everything the first C# provider does not reach.

## Resolved

- Provider selection through `<system.web><compilation><compilers>`, with no
  imported source change and therefore no ledger row.
- Reference set: shared framework implicitly, `<compilation><assemblies>` and
  `bin` through `CompilerParameters.ReferencedAssemblies`.
- Compiler options parsed by Roslyn's command-line parser; ASP.NET's warning
  policy reapplied in the provider.
- Diagnostics mapped through `#line` pragmas to the originating virtual path.
- C# only, `/langversion:7.3`, Visual Basic explicitly unsupported.
- `targetFramework` requires no portable capability discovery: the downlevel
  branches are design-time only, and ledger row P16 governs the upper bound.

## Open

- Visual Basic support, or a decision that it stays unsupported permanently.
- Custom and third-party `CodeDomProvider` implementations, including providers
  that still expect `CompileAssemblyFromDom` and provider options. .NET's
  `CompilerInfo` exposes no `ProviderOptions`, so `<providerOption>` elements are
  inert.
- Batch compilation behaviour: `batch`, `maxBatchSize`,
  `maxBatchGeneratedFileSize`, `batchTimeout`, and `maxConcurrentCompilations`.
- Linked resources, satellite culture assemblies, and
  `assemblyPostProcessorType`.
- Precompilation and the `ClientBuildManager` surface, which is the only consumer
  of multi-targeting and reference-assembly resolution.
- Cross-machine reproducible output, which needs `/pathmap` and deterministic
  emission together.

## Verification

Focused provider tests cover emission, mapped diagnostics, option passthrough,
language version, references, debug symbols, and the unsupported language. Record
deferred tests alongside the open items above.

## Done when

Every configuration an application can express either compiles or fails
actionably, and no supported configuration depends on a provider the port
selected implicitly.
