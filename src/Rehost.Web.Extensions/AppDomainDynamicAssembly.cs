using System;
using System.Collections.Generic;
using System.Reflection;

namespace System.Reflection.Emit;

internal static class AppDomainDynamicAssembly
{
    public static AssemblyBuilder DefineDynamicAssembly(
        this AppDomain domain,
        AssemblyName name,
        AssemblyBuilderAccess access,
        IEnumerable<CustomAttributeBuilder> assemblyAttributes)
    {
        return AssemblyBuilder.DefineDynamicAssembly(name, access, assemblyAttributes);
    }
}
