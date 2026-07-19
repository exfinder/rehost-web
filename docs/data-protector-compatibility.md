# `DataProtector` compatibility implementation

## Decision

`System.Security.Cryptography.DataProtector` is restored as an internal
compatibility implementation compiled directly into `System.Web`. It is not a
published compatibility contract. The managed base behavior uses
`SHA256.Create()` and `CryptographicOperations.FixedTimeEquals`; it has no
Windows or DPAPI runtime dependency. A concrete portable provider and its
key-persistence and wire-format policy require a separate ADR.

TODO: implement the concrete cross-platform provider after defining its key
persistence, rotation, and protected-payload format through an ADR and
differential tests.

## Authorities inspected

| Purpose | Artifact | SHA-256 |
| --- | --- | --- |
| API contract | .NET Framework 4.8.1 `System.Security.dll` reference assembly | `c867ee14fef732c5803c67a5a00797ef62080b29ce71f6fb14e1af325c8d2a3d` |
| Behavior | .NET Framework 4.8.1 runtime `System.Security.dll`, file version `4.8.9340.0` | `7d3ffef49454884e2e68e1713aac1d503e25b76b343396a3d7c54c175af59975` |
| Candidate implementation | POC commit `148b1aa30fe5229b41d6c53e24e4340350043f25`, later amended by `2d937d9` | `6b6810b7c9e334f53551aa90ffd8ffd9c9c44030f76c12e3e24f431ce12715f1` |

ILSpy `10.1.1.8388` was used to inspect the two framework assemblies. The
framework binaries are not redistributed by this repository.

## Deliberate portability changes

- `Sha256Cng` becomes `SHA256.Create()`; SHA-256 output remains identical.
- Framework's internal `SignedXml.CryptographicEquals` becomes public modern
  BCL `CryptographicOperations.FixedTimeEquals` with the same short-input guard.
- Local invariant messages reproduce the framework's English resource values;
  localization remains future work.
- The framework's public type becomes internal to `System.Web`; external custom
  subclasses and binary compatibility with `System.Security.dll` are outside
  this implementation's scope.
- `DpapiDataProtector` is not used as the portable default. Existing
  DPAPI-bound payloads require explicit Windows migration and re-protection.

## Verification

A focused executable check validated the known purpose hash, purpose prefix,
round trip, wrong-purpose rejection, and short-payload rejection before the
implementation was folded unchanged into `System.Web`. These checks must move
into the repository's differential test suite when its test infrastructure is
created.
