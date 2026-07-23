# Runtime code generation and loading

Status: open. Priority: high. Depends on host/runtime filesystem contract.

Modern AppDomain probing, dynamic-directory, and shadow-copy setters are
ineffective. Define project-owned codegen output, probing, cleanup, and loading
behavior without claiming Framework shadow-copy semantics.

Done when runtime compilation uses explicit paths, cleanup cannot escape its
disposable root, location/probing behavior is tested, and unsupported
shadow-copy expectations fail clearly.
