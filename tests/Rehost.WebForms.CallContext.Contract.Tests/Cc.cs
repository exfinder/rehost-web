// The subject under test, bound per target: mscorlib's CallContext on .NET Framework, the port's
// internal System.Runtime.Remoting.Messaging.CallContext (reached by reflection, since the type is
// internal to Rehost.WebForms.Runtime) on modern .NET. Same test bodies, two implementations.
namespace Rehost.WebForms.CallContext.Contract.Tests;

#if NET481
internal static class Cc
{
    public static string Implementation => "mscorlib 4.8.1";
    public static object? GetData(string name) => System.Runtime.Remoting.Messaging.CallContext.GetData(name);
    public static void SetData(string name, object? data) => System.Runtime.Remoting.Messaging.CallContext.SetData(name, data);
    public static object? LogicalGetData(string name) => System.Runtime.Remoting.Messaging.CallContext.LogicalGetData(name);
    public static void LogicalSetData(string name, object? data) => System.Runtime.Remoting.Messaging.CallContext.LogicalSetData(name, data);
    public static void FreeNamedDataSlot(string name) => System.Runtime.Remoting.Messaging.CallContext.FreeNamedDataSlot(name);
    public static object? HostContext
    {
        get => System.Runtime.Remoting.Messaging.CallContext.HostContext;
        set => System.Runtime.Remoting.Messaging.CallContext.HostContext = value;
    }
}
#else
using System.Reflection;

internal static class Cc
{
    private static readonly Type Type = typeof(System.Web.HttpContext).Assembly
        .GetType("System.Runtime.Remoting.Messaging.CallContext", throwOnError: true)!;

    private static readonly MethodInfo GetDataMethod = Method("GetData");
    private static readonly MethodInfo SetDataMethod = Method("SetData");
    private static readonly MethodInfo LogicalGetDataMethod = Method("LogicalGetData");
    private static readonly MethodInfo LogicalSetDataMethod = Method("LogicalSetData");
    private static readonly MethodInfo FreeNamedDataSlotMethod = Method("FreeNamedDataSlot");
    private static readonly PropertyInfo HostContextProperty =
        Type.GetProperty("HostContext", BindingFlags.Public | BindingFlags.Static)!;

    private static MethodInfo Method(string name) =>
        Type.GetMethod(name, BindingFlags.Public | BindingFlags.Static)!;

    public static string Implementation => "Rehost.WebForms.Runtime";
    public static object? GetData(string name) => GetDataMethod.Invoke(null, [name]);
    public static void SetData(string name, object? data) => SetDataMethod.Invoke(null, [name, data]);
    public static object? LogicalGetData(string name) => LogicalGetDataMethod.Invoke(null, [name]);
    public static void LogicalSetData(string name, object? data) => LogicalSetDataMethod.Invoke(null, [name, data]);
    public static void FreeNamedDataSlot(string name) => FreeNamedDataSlotMethod.Invoke(null, [name]);
    public static object? HostContext
    {
        get => HostContextProperty.GetValue(null);
        set => HostContextProperty.SetValue(null, value);
    }
}
#endif
