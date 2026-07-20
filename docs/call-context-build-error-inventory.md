# CallContext build inventory

| Measure | Before | After | Delta |
| --- | ---: | ---: | ---: |
| Errors | 27 | 14 | -13 |
| Warnings | 1,083 | 1,083 | 0 |

Removed group: `System.Runtime.Remoting.Messaging.CallContext`, 13 diagnostics.
No imported Reference Source changed. Remaining groups are outside this pass.

Reproduce:

```text
dotnet build src/Rehost.WebForms.Runtime/Rehost.WebForms.Runtime.csproj --no-restore --disable-build-servers --nologo --verbosity:quiet --maxcpucount:1 /p:UseSharedCompilation=false /nodeReuse:false -clp:ErrorsOnly
```
