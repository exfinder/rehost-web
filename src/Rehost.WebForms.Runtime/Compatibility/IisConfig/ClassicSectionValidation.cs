#nullable enable

using System.Collections.Generic;
using System.Configuration;
using System.Xml;

namespace System.Web.IisConfig;

// IIS's ConfigurationValidationModule rule (MH5, MH6, MH23): app-level classic registration
// content of any kind, or impersonation, fails every request with 500.22/500.23/500.24 unless the
// application declares validateIntegratedModeConfiguration="false". Only the application's own
// files are examined; the inherited classic defaults IIS ships are exempt.
//
// A folder web.config below the application root is examined the same way, with the waiver
// inherited from above unless the folder restates it. MH5 measured the application root; a
// folder-level classic section under the same rule is unmeasured.
internal static class ClassicSectionValidation
{
    private const string ValidationAttribute = "validateIntegratedModeConfiguration";

    internal static bool Validate(
        XmlDocument document, string configPath, bool inheritedWaiver = false)
    {
        if (IsWaived(document, configPath) ?? inheritedWaiver)
        {
            return true;
        }

        var entries = new List<string>();
        CollectSection(document, "httpModules", "name", entries);
        CollectSection(document, "httpHandlers", "path", entries);
        CollectImpersonation(document, entries);

        if (entries.Count == 0)
        {
            return false;
        }

        throw new ConfigurationErrorsException(
            "'" + configPath + "' carries classic-pipeline <system.web> configuration that"
            + " integrated mode does not run: " + string.Join(", ", entries)
            + ". IIS refuses every request to such an application (500.22/500.23/500.24 from"
            + " ConfigurationValidationModule). Register the entries under <system.webServer>,"
            + " or declare <validation " + ValidationAttribute + "=\"false\" /> under"
            + " <system.webServer> to keep the sections as dead text.");
    }

    // Inverted: validation="true" means IIS checks, so the application is NOT waived.
    private static bool? IsWaived(XmlDocument document, string configPath)
    {
        var validate = IisCollectionReader.OptionalBoolean(
            document.SelectSingleNode("/configuration/system.webServer/validation"),
            "validation",
            ValidationAttribute,
            configPath);

        return validate == null ? null : !validate.Value;
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

        // The referenced file is not opened: IIS refuses on the section being present at all, so
        // its content decides nothing, and the element carrying configSource has no children.
        var configSource = section.Attributes?["configSource"]?.Value;
        if (!string.IsNullOrEmpty(configSource))
        {
            entries.Add($"{sectionName} configSource=\"{configSource}\"");
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
