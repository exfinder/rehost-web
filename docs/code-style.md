# Code style

Rules for authored C#. Imported Reference Source keeps its original shape;
see [`provenance/`](provenance/).

Commit-message and comment rules stay in [`../AGENTS.md`](../AGENTS.md);
test-authoring rules are in [`writing-tests.md`](writing-tests.md).

## Locals

Prefer `var` over an explicit local variable type.

## Comments

The no-comments rule lives in [`../AGENTS.md`](../AGENTS.md), which every
session loads. Not repeated here: two copies drift.

## Multi-line string content

Config XML, markup, HTTP request heads, and expected output are raw string
literals. Concatenation with `+`, escaped quotes, and `\n` runs inside a
regular literal hide what the test actually sends.

```csharp
var app = WriteConfig(
    "web.config",
    """
    <modules>
      <remove name="Session" />
      <add name="Session" type="Probe.LogC" />
      <add name="LogA" type="Probe.LogA" />
    </modules>
    """);
```

Wire text carries its line endings explicitly, since the literal's endings
follow the file:

```csharp
var request = """
    GET /probe HTTP/1.1
    Host: localhost

    """.ReplaceLineEndings("\r\n");
```

Single-line content stays a single-line literal.
