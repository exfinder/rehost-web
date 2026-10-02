# Bringing up an application

1. **Import.** Copy source/content into apps, excluding outputs, restored packages,
   local databases and user settings. Record upstream identity and license in
   [sources](sources.md); preserve licenses/notices and keep Framework inputs buildable.
2. **Audit reached dependencies.** Classify shipped packages as usable, requiring
   recompilation/substitution, or outside the contract. Inspect build events,
   custom targets, generated assets and inherited Directory.Build settings.
   Put unresolved decisions in a follow-up indexed by [backlog](backlog.md).
3. **Resolve uncertain behavior.** Run a disposable Framework/IIS copy when needed.
   Capture the contract in executable fixtures/tests; historical reading tables
   and completed gap reports are unnecessary.
4. **Exercise dependencies.** Probe the paths the application invokes, not merely
   its references. A Framework-only facade can fail middleware initialization
   even when that same package's unused reference compiled elsewhere.
5. **Adapt narrow blockers.** Recompile System.Web consumers, modify imported code
   surgically and test changed behavior. Keep non-obvious rationale in an ADR;
   update compatibility only for verified current boundaries.
6. **Build consumer projects.** App compiles WAP sources; Host owns Kestrel and XDT.
   Web Sites ship runtime-compiled sources and need an explicit staging mode.
   Validate from packages outside the checkout as well as the repository feed.
7. **Validate platforms.** Journeys post to rendered actions and assert application
   outcomes. Run macOS, Linux and Windows rounds, then update current contracts
   and unresolved-work priority. Tests themselves carry evidence.

## Rebuild constraints

- Sidecars outside an imported tree do not inherit its Directory.Build settings.
  Repeat required constants and resource names deliberately.
- Pin a language version compatible with source and modern BCL overloads. C# 14
  array-to-span conversions can rebind Contains inside an expression tree to a
  ref-struct result that cannot be boxed; YAF libraries use C# 13 for this reason.
- Modern SmtpClient does not read Framework mailSettings. Configure delivery
  explicitly; assess other configuration-driven BCL types when reached.
- Reflective writes to initialized static readonly fields fail on modern .NET.
- Runtime mapping folds file/configuration casing, but application-built paths
  may still differ on case-sensitive filesystems; Linux validation catches them.

Workflow: [Windows validation](windows-validation-host.md), `eng/linux-round.sh`
and `eng/app-linux-smoke.sh <App> <port>`. App README owns setup; DEVELOPMENT owns
current contributor choices and unresolved application gaps.
