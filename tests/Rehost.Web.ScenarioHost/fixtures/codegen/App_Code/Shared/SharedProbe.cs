using System;

// Compiled into its own assembly because <codeSubDirectories> names this directory. The main
// App_Code assembly references it, which only works if this one is built first.
public static class SharedProbe
{
    public static string AssemblyName => typeof(SharedProbe).Assembly.GetName().Name;
}
