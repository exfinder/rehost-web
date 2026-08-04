using System.Text.Json;

namespace Rehost.WebForms.Parity.Harness;

public static class ParityJson
{
    public static readonly JsonSerializerOptions Options = new JsonSerializerOptions
    {
        WriteIndented = false
    };

    public static readonly JsonSerializerOptions IndentedOptions = new JsonSerializerOptions
    {
        WriteIndented = true
    };

    public static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, Options);
    }

    public static string SerializeIndented<T>(T value)
    {
        return JsonSerializer.Serialize(value, IndentedOptions);
    }

    public static T? Deserialize<T>(string json)
        where T : class
    {
        return JsonSerializer.Deserialize<T>(json, Options);
    }
}
