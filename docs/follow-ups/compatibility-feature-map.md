# Compatibility feature map

Status: open. Priority: high. Depends on explicit support decisions.

## Goal

Maintain a precise user-facing map of source/API and behavioral compatibility.
For each feature record: supported, partially supported, unsupported, or
unassessed; platforms; security/trust constraints; failure mode; and owning
contract/test.

## Rules

- “Supported” means portable tested behavior.
- Nothing is supported only on Windows.
- Partial support names the exact boundary.
- Unsupported behavior fails explicitly where reachable.
- Unassessed is not equivalent to unsupported or supported.
- Story completion updates the map; history stays in Git.

Implemented behavior remains canonical in feature contracts until this aggregate
map is complete.

## Shipped root configuration

The portable root web configuration is derived from the pinned .NET Framework
4.8.1 baseline. Two collections omit entries whose types this port does not
carry. A build provider resolves its type only when a file of that extension is
compiled, so a registered extension states nothing about whether the slice that
compiles it exists yet.

| Section | Omitted | Reason | Effect |
| --- | --- | --- | --- |
| `compilation/buildProviders` | `.edmx`, `.xoml`, `.svc`, `.xamlx` | Types live in `System.Data.Entity.Design`, `System.WorkflowServices`, `System.ServiceModel.Activation`, and `System.Xaml.Hosting` | Unsupported. Files of these types are ignored rather than reported against a Framework assembly that will never exist here |
| `pages/namespaces` | `System.Web.DynamicData` | Assembly absent | Generated code does not import it. Registering it would fail every compilation, not only code that uses it |
| `pages/controls` | all | Prefix registrations for markup | Deferred to the slice that compiles markup; the standard `asp:` prefix comes from assembly attributes, not this section |

`.wsdl` and `.xsd` stay registered under the type names Framework used, which
resolve here to the port's explicit refusal and its compatibility provider.

## Done when

The first runnable request and every encountered IIS/Windows feature have
entries suitable for a future README compatibility section.
