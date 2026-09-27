using System.CodeDom.Compiler;
using System.Web;

namespace Microsoft.Web.Infrastructure;

public static class InfrastructureHelper
{
    public static void UnloadAppDomain() => HttpRuntime.UnloadAppDomain();

    public static bool IsCodeDomDefinedExtension(string extension) =>
        CodeDomProvider.IsDefinedExtension(extension);
}
