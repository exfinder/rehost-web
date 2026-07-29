using System;
using System.CodeDom.Compiler;

namespace System.Web.Compilation;

internal sealed class RoslynCSharpCodeProvider : Microsoft.CSharp.CSharpCodeProvider
{
    // CompilationUtil.GetProviderOptions instantiates the configured type through
    // Activator.CreateInstance, which binds public constructors only.
    public RoslynCSharpCodeProvider()
    {
    }

    public override ICodeCompiler CreateCompiler() => new RoslynCSharpCompiler(this);
}
