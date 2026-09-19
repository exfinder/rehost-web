using System.Globalization;
using Humanizer;
using Newtonsoft.Json;

namespace SplitSample.HostPackages;

public static class HostProbe
{
    public static string Read()
    {
        var value = JsonConvert.DeserializeObject<int>("42");
        return value.ToWords(CultureInfo.GetCultureInfo("en"));
    }
}
