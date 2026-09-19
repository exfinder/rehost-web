using System.Globalization;
using System.Resources;
using Microsoft.Extensions.DependencyModel;

namespace SplitSample.Proj3;

public static class DependencyProbe
{
    public static string Read()
    {
        var dependency = new Dependency("nested-package", "1.2.3");
        return $"{dependency.Name}/{dependency.Version}";
    }

    public static string FrenchGreeting() => new ResourceManager("Proj3.Messages", typeof(DependencyProbe).Assembly)
        .GetString("Greeting", CultureInfo.GetCultureInfo("fr"))!;
}
