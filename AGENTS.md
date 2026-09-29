@PROJECT.md

## Commenting rules

DEFAULT: NO COMMENTS!

The rest of the comment rules: [`docs/code-comments.md`](docs/code-comments.md).

## C# code style rules

C# style rules are in [`docs/code-style.md`](docs/code-style.md); read it before writing code.

## Writing tests

How to choose the kind of test, the scenario act→assert shape, and the rest of
the test-authoring rules: [`docs/writing-tests.md`](docs/writing-tests.md).

## Architecture design principles

[`docs/architecture-design-principles.md`](docs/architecture-design-principles.md).

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

[`docs/cross-platform-validation.md`](docs/cross-platform-validation.md).
