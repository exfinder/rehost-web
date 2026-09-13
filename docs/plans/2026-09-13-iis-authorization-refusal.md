# IIS native authorization refusal

## Objective

Stop an application that locked a folder with IIS's native authorization from
starting here as an open folder. `system.webServer/security/authorization` and
`<handlers accessPolicy>` are unread today; IIS refused a request under either
ahead of managed code and identically in both pipeline modes
([MH36](../research/iis-modules-handlers-readings.md),
[IV29](../research/integrated-divergence-audit.md)). Neither is implemented by
this job: activation fails on sight, with the portable substitute named.

## Decisions

- Any `security/authorization` element in user configuration fails activation,
  whatever it holds. An empty section and an allow-everyone row fail too; the
  rule is presence, not content, so no row grammar is parsed.
- Any `accessPolicy` attribute on `<handlers>` in user configuration fails
  activation, whatever its value. The shipped baseline carries `Read, Script`
  itself and is never examined.
- User configuration is the application root `web.config`, every folder
  `web.config` the handler walk already reads, and `<location>` blocks in
  either.
- The message names the file, the element as written (`<authorization>` under
  `<system.webServer><security>`, or `<handlers accessPolicy="...">`), and the
  enclosing `<location path="...">` when there is one. One fixed tail follows:
  this is native IIS authorization, which the runtime does not enforce, so the
  path would serve openly; the portable equivalent is `system.web/authorization`
  in that folder's `web.config`, which guards managed handlers only, so static
  files still serve unless `runAllManagedModulesForAllRequests` is true
  (MH36, MH17).
- `ConfigurationErrorsException`, as the sibling sections throw.
- No reading or ledger IDs in code or config comments.

## Architecture

### Runtime

`IisConfig/NativeAuthorization.cs`, new: one static `Refuse(XmlDocument,
string configPath)`. Two XPaths over the whole document,
`//system.webServer/security/authorization` and
`//system.webServer/handlers[@accessPolicy]`, so a `<location>` block is
covered by the same query; the `location` ancestor supplies the path for the
message. Called from the application branch of
`IisServerConfiguration.ApplyFile`, beside `RewriteSection.Read`, and from
`IisFolderHandlers.Apply`, beside the two `RefuseBelowTheRoot` calls.

Nothing in Hosting changes. Nothing under `System.Web.ReferenceSource`
changes.

## Tests

Plain unit tests, `Runtime.Tests/Compatibility/IisConfig/NativeAuthorizationTests`,
same shape as `RequestLimitsTests`: a temp directory, a written `web.config`,
`IisServerConfiguration.Load` against the shipped baseline, no process.

1. Root `security/authorization` fails activation. Theory: an empty
   `<authorization />` and an `<add accessType="Allow" users="*" />` row.
2. Root `<handlers accessPolicy="Read">` fails activation.
3. A folder `web.config` carrying either element fails activation, naming the
   folder file. Theory over the two elements.
4. A `<location path="uploads">` block carrying either element fails
   activation, naming the location path. Theory over the two elements.
5. Control: a root `system.web/authorization` deny row plus a folder
   `<handlers>` section without `accessPolicy` loads. Pins the XPaths to
   `system.webServer`.

Every refusal asserts the file path and the element text case-sensitively.

## Docs

- Compatibility row "Native handler-execution and URL denial": Unsupported to
  Refused, with the rule and the message's substitute; row "URL/file
  authorization and impersonation" drops "native section is currently ignored".
- Ledger P103: unsupported, refused at activation; evidence index row names
  the test class.
- Backlog keeps the real tenant: per-path enforcement of both sections,
  covering static files, with MH36 and IV29.
- The divergence audit's IV29 row says refused, not unread.

## Sequence

1. Runtime class, the two call sites, the tests. macOS suite green.
2. Docs above.
3. Review against the pre-job commit, one fix commit, fast-forward main.
