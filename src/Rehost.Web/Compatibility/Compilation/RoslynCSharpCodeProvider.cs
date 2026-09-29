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

    // Long literal markup is otherwise emitted as a Win32 resource blob that
    // StringResourceManager.ReadSafeStringResource reads back through kernel32 and the module
    // image, which exists only on Windows. Reporting the capability as absent keeps the
    // generators on the literal-string path they already use for shorter markup.
    public override bool Supports(GeneratorSupport supports) =>
        supports is not GeneratorSupport.Win32Resources && base.Supports(supports);
}
