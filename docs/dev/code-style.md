# Code style

Rules for authored C#. Imported Reference Source keeps its original shape;
see [imported sources](sources.md). Keep portability edits surgical; the source
table changes only on imports or upgrades.

Comment rules are in [`code-comments.md`](code-comments.md); test-authoring
rules are in [`writing-tests.md`](writing-tests.md).

## Files

One top-level type per file, named for the type. Exception: a type and the
small types that exist only for it — a config section's child elements, a
private seam interface with its default implementation, marker/fixture
clusters — coupled enough that reading one without the others makes no sense.
Generated files are exempt, as is imported source (above).

## Locals

Prefer `var` over an explicit local variable type.

## Comments

Read [`code-comments.md`](code-comments.md).

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

## Commit messages

Use concise Conventional Commit subjects (`type: summary`).
Add a body only where the mechanism is not obvious from the diff. Existing commits carry long bodies; do not treat them as the standard.
Do not add `Co-Authored-By` footers.
