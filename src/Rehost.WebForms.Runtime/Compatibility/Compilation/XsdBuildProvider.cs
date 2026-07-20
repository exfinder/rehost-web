using System;

namespace System.Web.Compilation;

internal sealed class XsdBuildProvider : BuildProvider
{
    private const string Message = "App_Code typed DataSet generation from XSD is not supported by Rehost.WebForms.";

    public override void GenerateCode(AssemblyBuilder assemblyBuilder) =>
        throw new PlatformNotSupportedException(Message);
}
