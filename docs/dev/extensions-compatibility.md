# Extensions compatibility

Current support is in [compatibility](compatibility.md). `Rehost.Web.Extensions`
contains the ScriptManager/UpdatePanel stack, ScriptModule, ASMX JSON handlers and
proxy generation, disabled-by-default JSON profile/authentication/role services,
QueryExtender/QueryableDataSource and ListView/DataPager.

## Script contract

Thirteen release Microsoft AJAX scripts are generated from imported `.jsa`
recipes by `eng/GenerateAjaxScripts.cs` and committed under the Extensions project.
Generation uses AjaxMin 4.12, preserves the generated banner's CRLF and omits
DEBUGINTERNAL. ScriptResource.axd appends Sys.Res from ScriptLibrary resources per
request; physical-file mappings do not supply those strings.

Debug scripts require a separate validation-code generator absent from the source
snapshot, not another preprocessor run. Localized satellites are also absent.
ScriptMode.Auto falls back to invariant release resources. Partial rendering
requires a recognized browser; the default capability profile disables it.

Delta responses preserve their length framing, error tokens and encoded redirect
shape. Generated client blocks use the platform newline; encrypted resource tokens
and assembly timestamps are runtime-owned. Exception messages and types follow the
service contract while stack text follows the executing runtime.

## Exclusions

- LinqDataSource and its LINQ-to-SQL wrappers: System.Data.Linq is unavailable;
  declaring the control fails page compilation naming the missing type.
- WCF proxy/build-provider code generation: missing ServiceModel code-export APIs;
  use generated clients such as dotnet-svcutil output.
- WCF-hosted `.svc` application services: unavailable activation stack. JSON
  application-service routes remain separate.
- Client Services: Windows-desktop APIs and native dependencies.
- Upstream's unused PermaLink and duplicate LinqDataSourceContextData declarations.

Provider-backed JSON services are compiled but unassessed when enabled. Remaining
scope is in [Extensions follow-up](follow-ups/extensions-ajax-activation.md).
Imported input identity and licenses are in [sources](sources.md).
