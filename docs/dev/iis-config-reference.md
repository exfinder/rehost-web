# IIS configuration reference

The portable baseline is `src/Rehost.Web/configs/applicationHost.config`.
Its observable support belongs to [compatibility](compatibility.md); remaining
scope belongs to [IIS configuration tenants](follow-ups/iis-integration-plan.md).

For uncertain IIS configuration semantics, obtain applicationHost.config and
IIS_schema.xml from `Windows/System32/inetsrv/config` on a validation machine.
Local copies under `third_party/microsoft/iis-config` are ignored.
`eng/wire-rig/setup.ps1` provisions the IIS features for a behavioral comparison.

- applicationHost.config supplies MIME maps, hidden segments, default documents,
  request limits, module/handler registrations and native error defaults.
- IIS_schema.xml supplies collection keys, add/remove/clear shapes, attribute types
  and defaults absent from the server file. Schema alone cannot decide request
  behavior or scope; use executable checks when those contracts are uncertain.

A registered module name is behavioral identity: Global.asax binds Session_Start
and Session_End by the Session module name. Removing/re-adding the type must preserve
that binding unless the application deliberately changes the name.
