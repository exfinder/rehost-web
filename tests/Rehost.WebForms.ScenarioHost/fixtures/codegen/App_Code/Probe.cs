using System;
using System.Reflection;
using Rehost.WebForms.ScenarioProtocol;
using Rehost.WebForms.ScenarioProbes;

// Compiled at runtime into the App_Code assembly. It references a type from the sub-directory
// assembly and a generated global resource, so a run proves both were compiled before it and are
// visible to it.
public static class CodegenProbe
{
    public static void RecordCompiled()
    {
        TraceProbe.Record(TraceEvents.AppCode + typeof(CodegenProbe).Assembly.GetName().Name);
        TraceProbe.Record(TraceEvents.SubCode + SharedProbe.AssemblyName);
        TraceProbe.Record(TraceEvents.Resource + Resources.Strings.Greeting);
        RecordFixedName("App_Code");
        RecordFixedName("__code");
        RecordFixedName("App_SubCode_Shared");
        RecordFixedName("App_GlobalResources");
    }

    private static void RecordFixedName(string name)
    {
        string loaded;
        try
        {
            loaded = Assembly.Load(name).GetName().Name;
        }
        catch (Exception e)
        {
            loaded = e.GetType().Name;
        }

        TraceProbe.Record(TraceEvents.FixedName + name + "=" + loaded);
    }

    // ASP.NET calls this static method on the App_Code assembly before Application_Start.
    public static void AppInitialize()
    {
        TraceProbe.Record("app-initialize");
    }
}
