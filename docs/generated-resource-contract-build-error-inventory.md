# Generated resource contract build inventory

| Measure | Before | After | Delta |
| --- | ---: | ---: | ---: |
| Errors | 30 | 28 | -2 |
| Warnings | 1,083 | 1,083 | 0 |

Removed groups:

- `System.ComponentModel.DataAnnotations.Resources`: 1
- `System.Resources.Tools`: 1

All 28 remaining errors are outside scope: remoting/serialization,
EnterpriseServices, COM, XSD, and residual configuration. None were modified.

Reproduce:

```text
dotnet build src/Rehost.WebForms.Runtime/Rehost.WebForms.Runtime.csproj --no-restore --disable-build-servers --nologo --verbosity:quiet --maxcpucount:1 /p:UseSharedCompilation=false /nodeReuse:false -clp:ErrorsOnly
```
