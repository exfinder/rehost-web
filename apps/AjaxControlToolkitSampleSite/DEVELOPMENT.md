# AJAX Control Toolkit development

[Running guide](README.md) · [Imported sources](../../docs/dev/sources.md)

This is a Web Site: inline Global.asax, App_Code and CodeFile sources compile at
runtime. There is no App project. The Host references rebuilt Toolkit and
Sanitizer libraries, stages their dependencies into `rehost_root/bin/`, and
carries `StageWebSiteSources` because shared targets exclude WAP C# sources.
General Web Site packaging remains in [project models](../../docs/dev/follow-ups/web-site-vs-wap-project-models.md).

## Configuration choices

The Host XDT removes runtime/CodeDOM settings and Medium trust, inserts
`httpRuntime targetFramework="4.5"`, disables physical toolkit resources and
sets `renderStyleLinks=true`. Embedded scripts and styles therefore use
ScriptResource.axd and WebResource.axd.

The site's authored semantics are Framework 4.0, below the runtime's 4.5 floor.
Two choices preserve its intent:

- Keep the target at 4.5. At 4.7.2+, `WebObjectActivatorParseRecorder` emits a
  non-global `InfoBlock.InfoBlock` type reference that binds to the class instead
  of its namespace and fails compilation.
- Set `ValidationSettings:UnobtrusiveValidationMode=None`. The 4.5 default demands
  a jquery ScriptResourceMapping the site does not register.

The Host carries HtmlAgilityPack and the sanitizer in its dependency graph so
reflective sanitizer lookup can resolve the assembly. Design-time drawing types
are inert metadata; image processing gains no portable support.

## Remaining application gaps

`AjaxControlToolkit.StaticResources` is omitted: its bundles point at files the
Windows hard-link build tool would produce. Layout.master still references those
bundle paths, causing two 404s. Embedded delivery supplies the assets controls
need. Porting registration alone would create empty bundles; a portable resource
build step is required to supply the physical files.

The sanitizer consumer, upload journeys, Rating callbacks and obsolete Twitter
integration remain unassessed. UpdatePanel requests need a browser user agent
because partial rendering follows browser capabilities. Runtime claims belong
to [compatibility](../../docs/dev/compatibility.md).

```text
eng/app-linux-smoke.sh AjaxControlToolkitSampleSite 5084
```
