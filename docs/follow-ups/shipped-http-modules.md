# Shipped HTTP modules

Status: open. Priority: high. Depends on nothing; blocked on a rule, not on work.

## The gap

Framework's root web configuration registers fourteen modules
(`third_party/microsoft/framework-config/web.config:229-244`). The port's shipped
root configuration registers **none** — there is no `<httpModules>` element in
`configs/rehost-webforms.web.config` at all; only the section handler is declared
(`configs/rehost-webforms.machine.config:28`).

Three of the fourteen are explained by the rule the compatibility map already
applies to `httpHandlers` — their types live in assemblies this port does not
carry:

| Module | Assembly |
| --- | --- |
| `ErrorHandlerModule` | `System.Web.Mobile` |
| `ServiceModel` | `System.ServiceModel.Activation` |
| `ScriptModule-4.0` | `System.Web.Extensions` |

The other eleven are types the port **does** carry, absent with no recorded
reason: `OutputCache`, `Session`, `WindowsAuthentication`,
`FormsAuthentication`, `PassportAuthentication`, `RoleManager`,
`UrlAuthorization`, `FileAuthorization`, `AnonymousIdentification`, `Profile`,
`UrlRoutingModule-4.0`.

The compatibility map's "Shipped root configuration" table documents omissions
from `buildProviders`, `pages/namespaces`, `pages/controls`, `httpHandlers`, and
`browserCaps/result`. Dropping an entire collection is a larger divergence than
any row in that table and has no entry. Separately,
[minimal-pipeline-feature-profile.md](minimal-pipeline-feature-profile.md)
asserts that "Product defaults remain Framework-derived", which for this
collection is not currently true.

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

Either:

- **Ship Framework's list whole** (minus the three absent assemblies), accepting
  that eleven modules begin running on every request in a runtime that tests
  none of them; or
- **Grow the list one entry per landed story**, accepting that the shipped
  baseline is no longer Framework-derived and that each story must remember to
  add its row.

[Session state](session-state.md) takes the second shape for its own entry — it
adds `Session` when it lands — because it needed *an* answer for one module, not
because the general rule is settled. If the first shape is chosen instead, that
story's registration becomes redundant rather than wrong.

## What it costs today

Silence. An application configured for session state gets a null `Session` with
no diagnostic, because the module that would serve it is not registered. The
same is true of output caching, forms authentication, URL authorization, and
routing. [Deferred request surfaces](deferred-request-surfaces.md) rules out
exactly this: "Exclusion here does not authorize silent fallback."

Each absent module needs classifying the way the IIS-role audit classifies its
candidates — covered elsewhere, deliberately excluded, or missing and worth
restoring — rather than being absent by accident.

## Done when

- Every one of Framework's fourteen entries is either registered, recorded as
  assembly-absent, or recorded as a deliberate exclusion with a reason.
- The compatibility map's shipped-configuration table carries an `httpModules`
  row.
- `minimal-pipeline-feature-profile.md`'s "Framework-derived defaults" claim is
  either true or amended.
