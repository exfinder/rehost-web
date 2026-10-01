@PROJECT.md

## Commenting rules

DEFAULT: NO COMMENTS!

The rest of the comment rules: [`docs/dev/code-comments.md`](docs/dev/code-comments.md).

## C# code style rules

C# style rules are in [`docs/dev/code-style.md`](docs/dev/code-style.md); read it before writing code.

## Writing tests

How to choose the kind of test, the scenario act→assert shape, and the rest of
the test-authoring rules: [`docs/dev/writing-tests.md`](docs/dev/writing-tests.md).

## Architecture design principles

[`docs/dev/architecture-design-principles.md`](docs/dev/architecture-design-principles.md).

## Build and test commands

Example how to build the runtime project:

```text
dotnet build src/Rehost.Web
```

Run tests per project (or solution-wide — both are supported):

```text
dotnet test tests/Rehost.Web.Tests --no-build
```

### Codex CLI

If `dotnet build` hangs in Codex CLI, retry with `--maxcpucount:1`.

## Cross-platform validation

[`docs/dev/cross-platform-validation.md`](docs/dev/cross-platform-validation.md).
