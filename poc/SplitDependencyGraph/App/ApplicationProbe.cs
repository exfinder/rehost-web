using Newtonsoft.Json;
using SplitSample.Proj1;
using SplitSample.Proj3;

namespace SplitSample.App;

public static class ApplicationProbe
{
    public static Type SharedType => typeof(SharedValue);

    public static int Accept(SharedValue value) => value.Number * 2;

    public static object Read()
    {
        var newtonsoft = JsonConvert.DeserializeObject<Dictionary<string, int>>("""{"app":19}""")!;
        var textJson = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>("""{"text":37}""")!;
        return new
        {
            Newtonsoft = newtonsoft["app"],
            TextJson = textJson["text"],
            NestedProject = SharedValue.NormalizeVersion("2.3"),
            DependencyModel = DependencyProbe.Read(),
            FrenchResource = DependencyProbe.FrenchGreeting()
        };
    }
}
