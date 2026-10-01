# Golden Framework configuration reference

The pinned .NET Framework 4.8.1 configuration files, kept locally as the
comparison baseline for portable configuration work. They are the authority for
what a Framework application inherits before its own `web.config` is read.

Not committed: `third_party/microsoft/framework-config/` is git-ignored, because
these are verbatim Microsoft files. Pulled 2026-07-29 from
`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\Config` on the `winbox`
validation host — Framework release `533509`, `clr.dll` `4.8.9337.0`. 37 files,
640 KB, including `Browsers/`.

## What each file answers

`web.config.default` is the important one. The ASP.NET defaults live in the root
web configuration, not in `machine.config`; `machine.config` only declares the
section handlers. It supplies the golden `<compilation>` element — the
`<assemblies>` list an application inherits, `<buildProviders>`, and the
`<system.codedom><compilers>` entries with their `warningLevel` and
`providerOption` values.

`web.config.comments` and `machine.config.comments` document the default value of
every attribute, including those absent from the config itself. They are the only
written source for defaults that exist solely in Framework code.

`DefaultWsdlHelpGenerator.aspx` is a real, Microsoft-authored 70 KB page. It is a
useful compilation input once dynamic page compilation exists.

`web_*trust.config` and `legacy.web_*trust.config` are the named trust policy
files referenced by `<trustLevel>`. `Browsers/*.browser` are the browser
capability definitions behind `<browserCaps>`.

## Prefer the `.default` copies

Microsoft ships each config beside a pristine `.default`. Installers mutate the
live file; the `.default` is what shipped. On this host only `machine.config` and
`web.config` differ from their `.default` — every trust policy file is identical.

The live `web.config` differs substantively: Visual Studio appended
`Microsoft.VisualStudio.Web.PageInspector.Loader` *after* `<add assembly="*"/>`
inside `<assemblies>`, and added `Microsoft.VisualStudio.Enterprise.AspNetHelper`
to `<partialTrustVisibleAssemblies>`. Reading the live file would import a local
Visual Studio installation into the port's baseline. **Derive from `.default`.**

## Re-pulling

Requires the `winbox` host alias from `~/.ssh/config`. Read-only on the remote
side; it touches nothing but `$env:TEMP`.

```bash
D=third_party/microsoft/framework-config
mkdir -p "$D"

read -r -d '' PS <<'PWSH'
$ErrorActionPreference = 'Stop'
$d = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\Config'
$zip = Join-Path $env:TEMP 'fwconfig.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $d '*') -DestinationPath $zip -CompressionLevel Optimal
'===BEGIN zip==='
[Convert]::ToBase64String([IO.File]::ReadAllBytes($zip))
'===END zip==='
Remove-Item $zip -Force
PWSH

B64=$(printf '%s' "$PS" | python3 -c 'import base64,sys; sys.stdout.write(base64.b64encode(sys.stdin.read().encode("utf-16-le")).decode())')
ssh winbox "pwsh -NoProfile -EncodedCommand $B64" \
  | python3 -c 'import base64,re,sys; d=sys.stdin.read(); m=re.search(r"===BEGIN zip===\n(.*?)\n===END zip===", d, re.S); sys.stdout.buffer.write(base64.b64decode("".join(m.group(1).split())))' \
  > /tmp/fwconfig.zip

unzip -o -q /tmp/fwconfig.zip -d "$D"
```

The `pwsh -EncodedCommand` form is required: the ssh shell mangles quoting before
PowerShell sees it. PowerShell Core emits a `#< CLIXML` banner on stderr that the
extraction ignores.
