# Dynamic ASPX integration

Status: resolved. Slice 3 gate passes on macOS `arm64` and Windows `x64`.

The gate is port-local on both supported platforms, by
[ADR 0044](../adr/0044-gate-differentials-by-evidence-not-by-slice.md). Nothing
here replaces native or host-owned code, and the parser, control builders,
`HtmlTextWriter`, and `Page` lifecycle run as imported. The one changed condition
is `GeneratorSupport.Win32Resources`, and literal markup renders as the markup,
so its expectation follows from the fixture rather than from Framework.

## Fixture

`ScenarioHost/fixtures/page/`: `Default.aspx` with `CodeFile` code-behind,
`App_Code`, `Global.asax`, `web.config`, and `Default.expected.html`. No
precompiled page assembly, and no `<form runat="server">` — view state belongs to
a later slice, and `__VIEWSTATEGENERATOR` carries a deliberate P38 divergence
recorded in [machine key and view state](machine-key-and-viewstate-bootstrap.md).

The page carries literal markup including a block of 256 characters or more,
an expression, and `Label`, `Repeater`, `HyperLink`, `Image`, and `Panel`.
Values come from `App_Code` and `Global.asax`, so rendering also proves the
generated page assembly resolves both in one load context (P34).

## Verification

- runtime: cold request compiles and renders byte-for-byte as
  `Default.expected.html`; a warm request reuses the assembly rather than
  recompiling;
- runtime: the generated `App_Web_*` assembly references none of
  `WriteUTF8ResourceString`, `CreateResourceBasedLiteralControl`, or
  `SetStringResourcePointer` (ledger P39);
- hosting: real Kestrel in a child process serves the page over a socket and
  matches the same expected output, with expected status and `Content-Type` and
  clean shutdown.

Activation mutates process-global state, so every one of these runs through
`ScenarioHost` in a child process.

## Result

The tests pass from disposable application/codegen roots on both supported
platforms. Remaining exclusions are recorded in the
[compatibility feature map](compatibility-feature-map.md).
