# Web Site and Web Application Project packaging

Current support is in [compatibility](../compatibility.md).

## Ownership

WAP MSBuild compiles CodeBehind, designers, resources and Global.asax.cs into the
application assembly. Web Site runtime compiles CodeFile, App_Code, resources and
inline Global.asax; those sources must stay in the published site.

App_Code compiles into the WAP assembly by default; a Web Site shape sets
`RehostRuntimeCompiledAppCode=true` to leave it to the runtime.

Runtime targets own compile exclusions; hosting targets own staging, XDT and
publish layout. They need an explicit model to avoid omitted source or double
compilation. The Toolkit Host's StageWebSiteSources is the local workaround for
shared targets excluding C# content.

## Open contract

- Define deterministic WAP payload and package-only consumption. Existing staging
  excludes C# and transform files, publishes binaries under bin and applies
  configuration XDT before Rehost XDT; runtime compilation stays runtime-owned.
- Decide whether checked-in designer files remain inputs or regenerate without VS.
- Add a Web Site publish mode for App_Code, App_GlobalResources and CodeFile.
- Decide whether Web Site build ownership needs an [MSBuild SDK](rehost-sdk.md).

## Done when

Fresh package consumers publish both models without source omission or double
compilation, and tests exercise each ownership rule.
