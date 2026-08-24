# Migrating an application

Recipes for consumers moving a Web Forms application from Framework/IIS onto
this runtime. Each topic states the deployment shape first, then what must
carry over from the old hosting for behavior to survive the cutover.
[Compatibility](compatibility.md) owns the support claims.

## Machine keys

Pick by deployment shape:

- **Single instance on a machine or a container with a mounted volume** —
  declare nothing. Auto-generated keys persist in a per-application key file
  (host `MachineKeyDirectory` option, default `~/.rehost-webforms/machine-keys`),
  so restarts keep ViewState, forms tickets, and Katana cookies valid
  ([ADR 0010](adr/0010-machine-key-persistence.md)).
- **Containers and farms** — keep key attributes auto-generated in config and
  supply the keys through `REHOST_WEBFORMS_MACHINEKEY_VALIDATIONKEY` and
  `REHOST_WEBFORMS_MACHINEKEY_DECRYPTIONKEY`. Each variable substitutes the
  whole attribute string before parsing, exactly as if written in web.config.
  Set both variables or neither — a lone one fails at startup, as does an
  explicit configured key alongside a set variable.
- **Explicit `<machineKey>` keys in web.config** — supported unchanged.

Settings that lived in the server's root web.config (commonly the algorithms)
move into the application's own web.config; the shipped root configs carry
stock Framework defaults only:

```xml
<machineKey validation="HMACSHA256" decryption="AES" />
```

This leaves the key attributes at their `AutoGenerate` default, which both the
persisted key file and the environment variables fill. The same config then
serves local development (persisted autogen keys) and production (env keys)
without edits.

Carrying over from Framework:

- Ticket/ViewState continuity through the cutover requires the *effective*
  old values: explicit keys and algorithms as production actually resolved
  them, not as remembered. With `AutoGenerate`, there is nothing to carry —
  the DPAPI registry blob is unreadable here, outstanding payloads invalidate
  once, and users re-authenticate.
- `,IsolateApps` / `,IsolateByAppId` suffixes never interoperate across
  runtimes; mixed Framework/port farms must use bare keys
  ([machine key](follow-ups/machine-key-and-viewstate-bootstrap.md)).

## Generated output (codegen)

Framework's `Temporary ASP.NET Files` becomes a per-application directory the
host resolves at startup. Pick by deployment shape:

- **Local development and plain servers** — declare nothing. Output lands in
  `codegen` beside the host binaries, next to `configs`; it is safe to delete
  (the next start recompiles) and an unchanged application restarts without
  recompiling ([ADR 0008](adr/0008-codegen-storage.md)).
- **Containers** — a read-only root filesystem refuses the default at boot.
  Point `REHOST_WEBFORMS_COMPILATION_TEMPDIRECTORY` (or the host
  `CompilationTempDirectory` option) at a mounted writable path.

Say it in at most one place: the option, the variable, and a web.config
`<compilation tempDirectory>` that disagree fail at startup naming both
sources.

Carrying over from Framework: a `tempDirectory` attribute holding a Windows
path fails on Linux/macOS — remove it and use the default or the variable.

## Root configuration files

The runtime ships its own machine.config and root web.config (structurally
derived from Framework 4.8.1) in `configs` beside the host binaries. They are
frozen: there is no override, matching Framework, where the framework-install
root configs were outside an application's reach.

Anything your hosting environment carried in machine.config, the server's root
web.config, or applicationHost.config moves into the application's own
web.config, which inherits from the shipped baselines section by section —
the same channel Framework apps already used. The machine-key and codegen
topics above are instances of this rule.
