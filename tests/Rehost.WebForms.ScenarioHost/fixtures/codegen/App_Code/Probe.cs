using System;
using Rehost.WebForms.Parity.Contracts;
using Rehost.WebForms.ScenarioProbes;

// Compiled at runtime into the App_Code assembly. It references a type from the sub-directory
// assembly and a generated global resource, so a run proves both were compiled before it and are
// visible to it.
public static class CodegenProbe
{
    public static void RecordCompiled()
    {
        ScenarioJournal.Record(TraceEvents.AppCode + typeof(CodegenProbe).Assembly.GetName().Name);
        ScenarioJournal.Record(TraceEvents.SubCode + SharedProbe.AssemblyName);
        ScenarioJournal.Record(TraceEvents.Resource + Resources.Strings.Greeting);
    }

    // ASP.NET calls this static method on the App_Code assembly before Application_Start.
    public static void AppInitialize()
    {
        ScenarioJournal.Record("app-initialize");
    }
}
