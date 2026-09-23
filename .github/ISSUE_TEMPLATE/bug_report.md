---
name: Bug report
about: Something fails on the port that worked on .NET Framework, or the docs are wrong
title: ''
labels: bug
assignees: ''
---

**Where it fails**
Restore, build, startup, first request, or a later request. Paste the first
error line and the stack, not the last.

**Environment**
- OS and architecture (Windows x64, Linux x64, Linux arm64, macOS arm64):
- `dotnet --version`:
- Rehost package version (`Version="…"` in the App csproj):

**The application**
- Web Application Project or Web Site:
- Legacy target framework (`<httpRuntime targetFramework>`):
- `packages.config` lines involved, if any:
- Anything in `Web.Rehost.config` beyond the three default adjustments:

**To reproduce**
The smallest project that shows it, or an application under `apps/` in this
repository and the steps. If it depends on a database or a package you cannot
share, say so.

**Expected**
What .NET Framework or IIS did, if you know it.
