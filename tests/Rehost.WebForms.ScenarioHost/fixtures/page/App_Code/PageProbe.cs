using System.Collections.Generic;

// Compiled at runtime into the App_Code assembly. The page reads this list, so a rendered
// response proves the generated page assembly resolves an App_Code type, and records the order
// the top-level stages ran in.
public static class PageProbe
{
    public static readonly List<string> Stages = new List<string>();

    public static readonly string[] Items = { "alpha", "beta", "gamma" };

    // ASP.NET calls this static method on the App_Code assembly before Application_Start.
    public static void AppInitialize()
    {
        Stages.Add("app-initialize");
    }
}
