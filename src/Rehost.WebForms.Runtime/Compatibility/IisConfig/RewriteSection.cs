#nullable enable

using System.Collections.Generic;
using System.Xml;

namespace System.Web.IisConfig;

// The rule vocabulary is parsed by the host, which owns the URL before the managed pipeline, so
// the runtime carries the section's XML and nothing else. What it does own is the refusal: the
// parser accepts shapes it then drops in silence (outbound rules, server variables, duplicate
// names), and IIS answered those with a 500, so they stop activation here instead.
internal sealed class RewriteSection
{
    private RewriteSection(string xml, string configPath)
    {
        Xml = xml;
        ConfigPath = configPath;
    }

    internal string Xml { get; }

    internal string ConfigPath { get; }

    internal static RewriteSection? Read(XmlDocument document, string configPath)
    {
        RefuseIfPresent(
            document,
            "//location//rewrite",
            """<rewrite> inside <location>""",
            configPath,
            "is not honored: inbound rules are read from the application root <system.webServer>");

        var section = document.SelectSingleNode("/configuration/system.webServer/rewrite");
        if (section == null)
        {
            return null;
        }

        RefuseIfPresent(
            section,
            "globalRules",
            "<globalRules>",
            configPath,
            "is server-level configuration one application cannot carry");
        RefuseIfPresent(
            section,
            "outboundRules",
            "<outboundRules>",
            configPath,
            "is not supported: only inbound rules are honored");
        RefuseIfPresent(
            section,
            ".//serverVariables",
            "<serverVariables>",
            configPath,
            "is not supported: setting a server variable needs server-level allowedServerVariables");
        RefuseIfPresent(
            section,
            ".//rule[@patternSyntax='Wildcard']",
            """<rule patternSyntax="Wildcard">""",
            configPath,
            "is not supported: write the pattern in the default ECMAScript syntax");
        RefuseIfPresent(
            section,
            ".//action[@subStatusCode]",
            "<action subStatusCode>",
            configPath,
            "is not supported: the substatus is IIS-local detail no client reads");

        RefuseDuplicateNames(section, configPath);
        RefuseEscapingTargets(section, configPath);

        return new RewriteSection(section.OuterXml, configPath);
    }

    internal static void RefuseBelowTheRoot(XmlDocument document, string configPath) =>
        RefuseIfPresent(
            document,
            "//rewrite",
            "<rewrite>",
            configPath,
            "is honored only in the application root web.config");

    private static void RefuseDuplicateNames(XmlNode section, string configPath)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (XmlNode rule in section.SelectNodes(".//rule")!)
        {
            var name = rule.Attributes?["name"]?.Value;
            if (name != null && !seen.Add(name))
            {
                throw new InvalidOperationException(
                    $"""<rule name="{name}"> in '{configPath}' is declared twice; rule names are unique keys.""");
            }
        }
    }

    private static void RefuseEscapingTargets(XmlNode section, string configPath)
    {
        foreach (XmlNode action in section.SelectNodes(".//action[@type='Rewrite']")!)
        {
            var url = action.Attributes?["url"]?.Value;
            if (url == null)
            {
                continue;
            }

            if (url.StartsWith("/", StringComparison.Ordinal)
                || url.StartsWith("..", StringComparison.Ordinal)
                || Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                throw new InvalidOperationException(
                    $"""<action type="Rewrite" url="{url}"> in '{configPath}' leaves the application, """
                    + "which one process cannot serve; write the target application-relative.");
            }
        }
    }

    private static void RefuseIfPresent(
        XmlNode scope, string xpath, string element, string configPath, string reason)
    {
        if (scope.SelectSingleNode(xpath) != null)
        {
            throw new InvalidOperationException($"{element} in '{configPath}' {reason}.");
        }
    }
}
