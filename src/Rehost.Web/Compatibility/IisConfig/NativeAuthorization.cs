#nullable enable

using System.Configuration;
using System.Linq;
using System.Xml;

namespace System.Web.IisConfig;

// Only user configuration is examined: the shipped baseline carries accessPolicy itself, so
// calling this from the common path would refuse every application.
internal static class NativeAuthorization
{
    private const string Substitute =
        "is native IIS authorization, which this runtime does not enforce, so the path would serve"
        + " openly. The portable equivalent is <authorization> under <system.web> in that folder's"
        + " web.config, which guards managed handlers only, so static files still serve unless"
        + " runAllManagedModulesForAllRequests is true.";

    private static readonly string[] AcceptedFlags = ["Read", "Write", "Script", "Execute"];

    internal static void Refuse(XmlDocument document, string configPath)
    {
        var section = document.SelectSingleNode("//system.webServer/security/authorization");
        if (section != null)
        {
            throw Refusal(
                "<authorization> under <system.webServer><security>", section, configPath);
        }

        var policies = document.SelectNodes("//system.webServer/handlers[@accessPolicy]")!;
        foreach (XmlNode handlers in policies)
        {
            var policy = handlers.Attributes!["accessPolicy"]!.Value;
            if (!IsReadWriteScriptExecute(policy))
            {
                throw Refusal($"""<handlers accessPolicy="{policy}">""", handlers, configPath);
            }
        }
    }

    private static bool IsReadWriteScriptExecute(string policy)
    {
        var flags = policy.Split(',', StringSplitOptions.TrimEntries);

        return flags.All(flag => AcceptedFlags.Contains(flag, StringComparer.OrdinalIgnoreCase))
            && AcceptedFlags.All(flag => flags.Contains(flag, StringComparer.OrdinalIgnoreCase));
    }

    private static ConfigurationErrorsException Refusal(
        string element, XmlNode match, string configPath)
    {
        var path = match.SelectSingleNode("ancestor::location")?.Attributes?["path"]?.Value;
        var location = path == null ? string.Empty : $""" inside <location path="{path}">""";

        return new ConfigurationErrorsException(
            $"{element}{location} in '{configPath}' {Substitute}");
    }
}
