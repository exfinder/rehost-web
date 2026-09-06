using System.Reflection;
using System.Reflection.Emit;

public static class AppDomainDynamicAssemblyExtensions
{
    public static AssemblyBuilder DefineDynamicAssembly(this AppDomain domain, AssemblyName name, AssemblyBuilderAccess access) =>
        AssemblyBuilder.DefineDynamicAssembly(name, access);
}
