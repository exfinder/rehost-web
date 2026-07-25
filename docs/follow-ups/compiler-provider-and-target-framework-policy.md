# Compiler provider and target-framework policy

Status: open. Priority: high. Depends on runtime codegen/loading.

## Problem

Dynamic page compilation historically selects configured CodeDOM providers,
Framework targets, imports, references, and compiler options. Hardcoding a C#
provider or treating .NET Framework names as installed on non-Windows makes a
demo run but changes the application contract. Registry discovery is not
portable.

## Required decisions

- Supported languages/providers for the first milestone and explicit rejection
  for others.
- Provider configuration, deployment, lifetime, and compiler-server policy.
- Interpretation of `targetFramework`, legacy compiler versions, references,
  imports, resources, debug settings, and warning options.
- Portable target capability discovery without registry probes.
- Diagnostic mapping back to source/page locations.

## Verification

The first C# fixture gets focused compiler-service tests. Record deferred tests
for VB, custom providers, options, resources, batch compilation, and failures.

## Done when

C# page compilation works through an explicit extensible contract, not a
hardcoded bypass, and unsupported configurations fail actionably.
