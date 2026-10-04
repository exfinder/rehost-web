# BlogEngine redistribution notices

These files accompany the sample site's dependencies. Site staging
copies this directory into build and publish output. BlogEngine's source and
the modified files in the sidecar Core project remain under
[MS-RL](LICENSE-MS-RL.txt).

| Component | Version | Terms |
| --- | --- | --- |
| AjaxMin | 4.6.3932.19636 | [Apache-2.0](LICENSE-AjaxMin.txt) |
| BlogML | 2.5.3.0 | [BSD-3-Clause](LICENSE-BlogML.txt) |
| SharpZipLib | 1.4.2 | [MIT](LICENSE-SharpZipLib.txt) |

AjaxMin and BlogML are unchanged binaries from
[BlogEngine.NET v3.3.8.0](https://github.com/BlogEngine/BlogEngine.NET/tree/e81ba7425e860c2265f66911436bd241061274f9/lib).
The source license evidence is:

- AjaxMin's historical CodePlex source, preserved in the
  [4.6 version commit](https://github.com/arcdev/ajaxmin-fromcodeplex/blob/97e88f0864029f02f1fe7c0627702e8b9f6c9f81/Properties/AssemblyVersion.cs).
  Its header grants Apache-2.0 and declares `AssemblyVersion("4.6.*")`.
  The binary's copyright is Microsoft 2010. The license text is copied from
  [Apache](https://www.apache.org/licenses/LICENSE-2.0.txt); the modern AjaxMin
  repository's MIT license is not used for this binary.
- BlogML's BSD notice, preserved by the .NET Core port of the original
  CodePlex .NET library in its
  [initial import](https://github.com/hmobius/BlogML.Core/blob/bfb15873540b903cda35292b64add23343a84c9e/LICENSE).
  The notice retains MarkItUp.com's attribution. The binary separately
  declares `Copyright 2005-2007 BlogML Project`, also retained here.
- SharpZipLib restores from the
  [1.4.2 NuGet package](https://www.nuget.org/packages/SharpZipLib/1.4.2).
  The package declares MIT and pins source revision `33f64eb`. Its license
  text is copied unchanged from that revision's
  [`LICENSE.txt`](https://github.com/icsharpcode/SharpZipLib/blob/33f64eb0f28cdd2b084cb822fcc224c7c5aba553/LICENSE.txt).

## SyntaxHighlighter and XRegExp

The adjacent script tree carries
[MIT notices](../Scripts/syntaxhighlighter/LICENSE-MIT.txt) for SyntaxHighlighter
3.0.83 and XRegExp 1.5.1. The license choice comes from BlogEngine's omitted
[SyntaxHighlighter distribution header](https://github.com/BlogEngine/BlogEngine.NET/blob/e81ba7425e860c2265f66911436bd241061274f9/lib/syntaxhighlighter_3.0.83/scripts/shCore.js),
which states dual MIT/GPL licensing, and the
[XRegExp 1.5.1 source header](https://github.com/slevithan/xregexp/blob/a6acc474eb4b00b8e4c52ace98148efa92b43838/src/xregexp.js),
which states MIT. Both copyright notices are retained.
