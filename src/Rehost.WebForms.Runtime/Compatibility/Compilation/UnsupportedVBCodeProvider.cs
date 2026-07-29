using System;
using System.CodeDom;
using System.CodeDom.Compiler;

namespace System.Web.Compilation;

internal sealed class UnsupportedVBCodeProvider : Microsoft.VisualBasic.VBCodeProvider
{
    internal const string UnsupportedMessage =
        "Visual Basic page compilation is not supported by Rehost.WebForms. Only C# is implemented. " +
        "Set <compilation defaultLanguage=\"c#\"> or declare Language=\"C#\" on the page.";

    // CompilationUtil.GetProviderOptions instantiates the configured type through
    // Activator.CreateInstance, which binds public constructors only.
    public UnsupportedVBCodeProvider()
    {
    }

    public override ICodeCompiler CreateCompiler() => new UnsupportedCompiler();

    private sealed class UnsupportedCompiler : ICodeCompiler
    {
        public CompilerResults CompileAssemblyFromDom(CompilerParameters options, CodeCompileUnit compilationUnit) =>
            throw new PlatformNotSupportedException(UnsupportedMessage);

        public CompilerResults CompileAssemblyFromDomBatch(CompilerParameters options, CodeCompileUnit[] compilationUnits) =>
            throw new PlatformNotSupportedException(UnsupportedMessage);

        public CompilerResults CompileAssemblyFromFile(CompilerParameters options, string fileName) =>
            throw new PlatformNotSupportedException(UnsupportedMessage);

        public CompilerResults CompileAssemblyFromFileBatch(CompilerParameters options, string[] fileNames) =>
            throw new PlatformNotSupportedException(UnsupportedMessage);

        public CompilerResults CompileAssemblyFromSource(CompilerParameters options, string source) =>
            throw new PlatformNotSupportedException(UnsupportedMessage);

        public CompilerResults CompileAssemblyFromSourceBatch(CompilerParameters options, string[] sources) =>
            throw new PlatformNotSupportedException(UnsupportedMessage);
    }
}
