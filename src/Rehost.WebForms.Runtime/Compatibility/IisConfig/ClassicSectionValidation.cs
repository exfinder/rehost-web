#nullable enable

using System.Collections.Generic;
using System.Configuration;
using System.Xml;

namespace System.Web.IisConfig;

// IIS's ConfigurationValidationModule rule (MH5, MH6, MH23): app-level classic registration
// content of any kind, or impersonation, fails every request with 500.22/500.23/500.24 unless the
// application declares validateIntegratedModeConfiguration="false". Only the application's own
// file is examined; the inherited classic defaults IIS ships are exempt.
internal static class ClassicSectionValidation
{
    private const string ValidationAttribute = "validateIntegratedModeConfiguration";

    internal static void Validate(XmlDocument document, string configPath)
    {
        if (IsWaived(document, configPath))
        {
            return;
        }

        var entries = new List<string>();
        CollectSection(document, "httpModules", "name", entries);
        CollectSection(document, "httpHandlers", "path", entries);
        CollectImpersonation(document, entries);

        if (entries.Count == 0)
        {
            return;
        }

        throw new ConfigurationErrorsException(
            "'" + configPath + "' carries classic-pipeline <system.web> configuration that"
            + " integrated mode does not run: " + string.Join(", ", entries)
            + ". IIS refuses every request to such an application (500.22/500.23/500.24 from"
            + " ConfigurationValidationModule). Register the entries under <system.webServer>,"
            + " or declare <validation " + ValidationAttribute + "=\"false\" /> under"
            + " <system.webServer> to keep the sections as dead text.");
    }

    private static bool IsWaived(XmlDocument document, string configPath)
    {
        var value = document
            .SelectSingleNode("/configuration/system.webServer/validation")
            ?.Attributes?[ValidationAttribute]?.Value;

        if (value == null || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        throw new ConfigurationErrorsException(
            "<validation " + ValidationAttribute + "=\"" + value + "\"> in '" + configPath
            + "' is not a boolean; IIS accepts only \"true\" or \"false\".");
    }

    private static void CollectSection(
        XmlDocument document,
        string sectionName,
        string keyAttribute,
        List<string> entries)
    {
        var section = document.SelectSingleNode("/configuration/system.web/" + sectionName);
        if (section == null)
        {
            return;
        }

        foreach (XmlNode node in section.ChildNodes)
        {
            if (node.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            var key = node.Attributes?[keyAttribute]?.Value;
            entries.Add(
                sectionName + " <" + node.Name
                + (key == null ? "" : " " + keyAttribute + "=\"" + key + "\"") + ">");
        }
    }

    private static void CollectImpersonation(XmlDocument document, List<string> entries)
    {
        var impersonate = document
            .SelectSingleNode("/configuration/system.web/identity")
            ?.Attributes?["impersonate"]?.Value;

        if (string.Equals(impersonate, "true", StringComparison.OrdinalIgnoreCase))
        {
            entries.Add("<identity impersonate=\"true\">");
        }
    }
}
