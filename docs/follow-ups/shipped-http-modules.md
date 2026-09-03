# Shipped HTTP modules

## Where registration stands

The registration question is settled. The port models an integrated application
pool, so the shipped `applicationHost.config` baseline carries the IIS golden's
`<modules>` collection whole, in its measured order (MH1), and the classic
`<httpModules>` table it used to ship is retired (ledger P83). Framework's
fourteen classic entries are no longer the list to reconcile against: the golden
names thirteen managed modules, and every one of them registers.

Two consequences are carried deliberately:

- `WindowsAuthentication` and `FileAuthorization` register faithfully and are
  inert (ledger P84). Reaching their Windows-bound behavior fails actionably
  rather than diverging silently; `<authentication mode="Windows">` is refused
  at activation. One exception: the public
  `FileAuthorizationModule.CheckFileAccessForUser` grants access silently,
  recorded in the [compatibility map](../compatibility.md)'s URL/file
  authorization row.
- `DefaultAuthentication` comes from its golden row rather than from
  `HttpModulesSection.CreateModules`' implicit append.

`ErrorHandlerModule` (`System.Web.Mobile`) and `ServiceModel`
(`System.ServiceModel.Activation`) are absent from the golden's managed set and
from this port, for the same reason: their assemblies are not carried.

## What registration does not settle

A module in the collection runs on every request, so registration is a
behavioral claim in a way handler registration is not — but it is not a *feature*
claim. What the authentication, roles, profile and anonymous-identity modules
actually support is stated by the [compatibility map](../compatibility.md)'s own
rows and by [forms authentication](forms-authentication.md); the rows behind
them stay Partial until a real application reaches them.

One lesson worth keeping for anyone amending the baseline: a name in this
collection is not only an identifier. `Global.asax` binds
`Session_Start`/`Session_End` by matching the registered module name, so the name
is part of the behavior. The same is why an application's `remove`/re-add of
`Session` puts a foreign type in the inherited slot without renaming it (MH2).

## Done when

- The feature rows behind the registered modules carry their own claims, or the
  modules are recorded as deliberate exclusions with reasons.
