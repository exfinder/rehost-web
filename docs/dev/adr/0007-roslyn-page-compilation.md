# Roslyn page compilation

`CSharpCodeProvider.CompileAssemblyFromFile` throws `PlatformNotSupportedException`
on .NET, so nothing emits a generated assembly. Supply a `CodeDomProvider` that
compiles with Roslyn in process, and select it through configuration rather than
by hardcoding a provider in imported source.

## Selection

The provider derives from `Microsoft.CSharp.CSharpCodeProvider` and overrides
`CreateCompiler` only; code generation, `FileExtension`, and identifier escaping
remain the inherited implementations, which are unaffected.

Framework declared its compilers in `<system.codedom>`. .NET's `System.CodeDom`
carries a fixed compiler table and no configuration handler, so that section is
unreadable here. The ASP.NET `<system.web><compilation><compilers>` collection is
the remaining seam, and the shipped root web configuration declares the providers
there. Applications override or replace the entries as they always could. No
imported source changes.

Two constraints keep selection on the configured type. The provider declares a
public parameterless constructor, because `CompilationUtil.GetProviderOptions`
instantiates the configured type through `Activator.CreateInstance`. It declares
no `IDictionary<string, string>` constructor, because that path would call
`CodeDomProvider.CreateProvider` and return the built-in provider instead.

## References

The compiler references every assembly in the shared framework directory of the
running runtime, then everything `CompilerParameters.ReferencedAssemblies`
supplies. ASP.NET resolves that collection from `<compilation><assemblies>`, the
application `bin` directory, and top-level generated assemblies, so the declared
application contract is preserved.

Framework inherited 25 coarse assemblies covering substantially the whole
Framework class library. .NET splits that same surface across roughly 170
assemblies, and the assemblies Framework deliberately excluded are the ones
absent from the shared framework as well. Referencing the shared framework is
therefore the closest image of the inherited list, not a widening. Enumerating a
curated subset is not viable: the compatibility facades forward to
implementation assemblies that must also be referenced, and `System.Private.*`
assemblies are among them.

The shipped root web configuration declares what Framework inherited that the
shared framework does not provide: the three port assemblies standing in for
`System.Web`, `System.Web.Services`, and `System.Web.ApplicationServices`, plus
`System.Configuration.ConfigurationManager`, `System.Drawing.Primitives`, and
`System.Data.SqlClient`.

## Options and diagnostics

`CompilerParameters.CompilerOptions` is parsed by Roslyn's own command-line
parser, so `/define:`, `/langversion:`, `/nowarn:`, and `/warnaserror` behave as
`csc` defines them.

`AssemblyBuilder.FixUpCompilerParameters` applies ASP.NET's warning policy only
when the provider type is exactly `Microsoft.CSharp.CSharpCodeProvider`, which a
derived provider never matches. The compiler therefore reapplies the suppression
list itself, and ignores `CompilerParameters.TreatWarningsAsErrors`, which the
`<compiler>` element sets from `warningLevel > 0` and would otherwise make every
warning fatal.

Diagnostics are reported from the mapped source span, so the `#line` pragmas the
code generators emit resolve errors to the `.aspx` an author wrote rather than to
generated source. `ErrorFormatter` and `FixUpLinePragmas` both depend on that.
Only warnings and errors reach `CompilerResults`.

## Language support

C# only. The default language stays Framework's `vb`, so a page that omits
`Language` still means what it always meant; Visual Basic is registered as an
explicitly unsupported provider that fails naming the limitation and the fix.
Leaving it unregistered would fall through to the built-in provider and throw
`PlatformNotSupportedException` with no explanation.

`/langversion:7.3` is the shipped default, matching what Microsoft's shipping
Roslyn provider writes into a Web Forms site. Later versions introduce source
breaking changes against legacy code, including the C# 14 `field` keyword inside
property accessors. Applications opt into newer versions through
`compilerOptions`.

## Boundaries

The output path is an input: the compiler writes where
`CompilerParameters.OutputAssembly` points and never consults
`HttpRuntime.CodegenDir`. Codegen generation isolation stays independent of this
work.

Emission is not deterministic, matching `csc` without `/deterministic`.

`targetFramework` needs no handling. `MultiTargetingUtil` resolves to 4.0 or the
validated configured value, and its 2.0 and 3.5 branches require
`BuildManagerHost.SupportsMultiTargeting`, which is a design-time flag. Values
above the implemented surface fail during target validation.
