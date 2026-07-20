namespace System.Web.Compilation;

internal abstract class BuildProvider
{
    public abstract void GenerateCode(AssemblyBuilder assemblyBuilder);
}

internal sealed class AssemblyBuilder
{
}
