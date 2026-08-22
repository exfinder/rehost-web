# Shipped HTTP modules

## The gap

Framework's root web configuration registers fourteen modules
(`third_party/microsoft/framework-config/web.config:229-244`). The port now
registers `Session`, `UrlAuthorization`, `UrlRoutingModule-4.0`, and
`ScriptModule-4.0` at their Framework positions. The remaining entries are
still classified here.

Two of the fourteen are explained by the rule the compatibility map already
applies to `httpHandlers` — their types live in assemblies this port does not
carry:

| Module | Assembly |
| --- | --- |
| `ErrorHandlerModule` | `System.Web.Mobile` |
| `ServiceModel` | `System.ServiceModel.Activation` |

The other eight are types the port **does** carry. `OutputCache`,
`FormsAuthentication`, `RoleManager`, `AnonymousIdentification`, and `Profile`
are now registered by the [forms authentication](forms-authentication.md)
slice. `WindowsAuthentication`, `PassportAuthentication`, and
`FileAuthorization` stay out with the reasons recorded there, and
`<authentication mode="Windows">` is refused at activation rather than being
silently inert.

`DefaultAuthentication` is not one of the fourteen and is not at stake here:
`HttpModulesSection.CreateModules` appends it to every module collection, as on
Framework, so requests already get its anonymous principal.

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

The general policy remains undecided. Four entries have landed —
`UrlAuthorization`, `UrlRoutingModule-4.0`, `Session`, and `ScriptModule-4.0`
(which left the assembly-absent class when `Rehost.WebForms.Extensions` was
carried) — each because a behavior story reached and tested it, preserving
Framework order. That is the pattern so far, not a decision that the remaining
baseline should land whole.

`Session` carries one lesson for whoever decides the general policy: a module
name in this collection is not only an identifier. `Global.asax` binds
`Session_Start`/`Session_End` by matching it, so the registered name is part of
the behavior, not merely a label.

## What it costs today

Session state, output caching, and forms authentication were each the worked
example of the cost, and each is now fixed: the modules that would serve them
were not registered and no diagnostic said so, which the project contract does
not permit.

What remains is the general policy. Each absent module needs classifying the
way the IIS-role audit classifies its candidates — covered elsewhere,
deliberately excluded, or missing and worth restoring — rather than being
absent by accident.

## Done when

- Every one of Framework's fourteen entries is either registered, recorded as
  assembly-absent, or recorded as a deliberate exclusion with a reason.
- The compatibility map's shipped-configuration row is updated.
