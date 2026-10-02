# Roadmap

Development follows real applications. The next priorities are easier adoption
and a production deployment baseline. See [what works](docs/what-works.md)
for current compatibility.

## Current

Make Rehost.Web easier to try with existing applications:

- Finish packaging, publication, and licensing.
- Improve setup, migration guides, and examples.

Follow the [public release milestone](https://github.com/exfinder/rehost-web/milestone/1)
for progress. More application ports and the optional migration helper do not
block this release.

## Next

Establish a production deployment baseline for a running application:

- Reliable Windows/Linux publishing and containers.
- Graceful shutdown, readiness checks, metrics, and diagnostics.
- External configuration, secrets, and multiple instances.

## Later

Expand compatibility through additional representative applications and
address the gaps they reveal.

Broader migration automation remains in the backlog.

## Scope

- Progress comes from running applications; complete `System.Web` coverage is
  not a prerequisite.
- Libraries bound to the .NET Framework's `System.Web` require recompilation
  or replacement packages.
- The runtime stays portable across Windows, Linux, and macOS.
- Application ports aim to preserve existing source.

See the [development backlog](docs/dev/backlog.md) for detailed tasks. This
roadmap sets milestone order and direction.
