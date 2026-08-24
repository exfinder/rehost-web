# Code style

Rules for authored C#. Imported Reference Source keeps its original shape;
see [`provenance/`](provenance/).

Commit-message and comment rules stay in [`../AGENTS.md`](../AGENTS.md);
test-authoring rules are in [`writing-tests.md`](writing-tests.md).

## Files

One top-level type per file, named for the type. Exception: a type and the
small types that exist only for it — a config section's child elements, a
private seam interface with its default implementation, marker/fixture
clusters — coupled enough that reading one without the others makes no sense.
Generated files are exempt, as is imported source (above).

## Locals

Prefer `var` over an explicit local variable type.

## Comments

The no-comments rule lives in [`../AGENTS.md`](../AGENTS.md), which every
session loads. Not repeated here: two copies drift.

## Building a string

Interpolate values; do not splice them with `+`. A message assembled as
`"<" + name + "=\"" + value + "\">"` hides its own shape, and every escaped
quote is a chance to lose one.

```csharp
$"""<{elementName} {attribute}="{value}"> in '{configPath}' {BooleanRule}"""
```

A raw interpolated literal carries the quotes an IIS or XML message needs
without escapes. Where a message would then run past the line budget, put the
invariant tail in a `const` rather than reintroducing value splicing.

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
