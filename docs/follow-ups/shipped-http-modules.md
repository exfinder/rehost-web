# Shipped HTTP modules

## The gap

Framework's root web configuration registers fourteen modules
(`third_party/microsoft/framework-config/web.config:229-244`). The port now
registers `UrlAuthorization` and `UrlRoutingModule-4.0` at their Framework
positions. The remaining entries are still classified here.

Three of the fourteen are explained by the rule the compatibility map already
applies to `httpHandlers` — their types live in assemblies this port does not
carry:

| Module | Assembly |
| --- | --- |
| `ErrorHandlerModule` | `System.Web.Mobile` |
| `ServiceModel` | `System.ServiceModel.Activation` |
| `ScriptModule-4.0` | `System.Web.Extensions` |

The other nine are types the port **does** carry, absent with no recorded
reason: `OutputCache`, `Session`, `WindowsAuthentication`,
`FormsAuthentication`, `PassportAuthentication`, `RoleManager`,
`FileAuthorization`, `AnonymousIdentification`, and `Profile`.

The [compatibility map](../compatibility.md) documents omissions from
`buildProviders`, `pages/namespaces`, `pages/controls`, and `httpHandlers`.
Dropping an entire collection is a larger divergence; the rest of the product
baseline is Framework-derived, while this collection is the known exception.

## Why the existing rule does not transfer

The port ships Framework's `buildProviders` and `httpHandlers` collections whole,
minus assembly-absent entries, and the map states the justification directly: a
registered build provider "states nothing about whether the slice that compiles
it exists yet." That holds because a provider or handler only executes when a
matching file or URL arrives.

**A module in `<httpModules>` runs on every request.** Registration is therefore
a behavioral claim in a way handler registration is not, and the "ship it whole"
precedent cannot simply be extended.

## The decision

The general policy remains undecided. This story registered
`UrlAuthorization` and `UrlRoutingModule-4.0` because both were reached and
tested, preserving Framework order. That does not decide whether the remaining
baseline should eventually land whole or one behavior story at a time.

[Session state](session-state.md) owns its future entry unless the broader
policy is decided first.

## What it costs today

An application configured for session state gets a null `Session` with
no diagnostic, because the module that would serve it is not registered. The
same is true of output caching and forms authentication. The project contract
does not permit that silent fallback.

Each absent module needs classifying the way the IIS-role audit classifies its
candidates — covered elsewhere, deliberately excluded, or missing and worth
restoring — rather than being absent by accident.

## Done when

- Every one of Framework's fourteen entries is either registered, recorded as
  assembly-absent, or recorded as a deliberate exclusion with a reason.
- The compatibility map's shipped-configuration row is updated.
