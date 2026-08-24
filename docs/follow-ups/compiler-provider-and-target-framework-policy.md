# Compiler provider and target-framework policy

The provider contract is decided and implemented; see
[Roslyn page compilation](../adr/0007-roslyn-page-compilation.md).
What remains is everything the first C# provider does not reach.

## Open

- Visual Basic support, or a decision that it stays unsupported permanently.
- Custom and third-party `CodeDomProvider` implementations, including providers
  that still expect `CompileAssemblyFromDom` and provider options. .NET's
  `CompilerInfo` exposes no `ProviderOptions`, so `<providerOption>` elements are
  inert.
- Batch compilation behaviour: `batch`, `maxBatchSize`,
  `maxBatchGeneratedFileSize`, `batchTimeout`, and `maxConcurrentCompilations`.
  Satellite assemblies already compile through `Parallel.ForEach` under
  `maxConcurrentCompilations`, but no test drives more than one culture.
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
