#nullable enable

using System.Collections.Generic;
using System.Configuration;
using System.Xml;

namespace System.Web.IisConfig;

// One <add> row of system.webServer/modules or /handlers. Attributes stay verbatim: a handler
// row's modules= names native IIS modules and the native bridge resolves it, not this seam.
internal sealed class IisRegistration
{
    private readonly Dictionary<string, string> _attributes;
    private readonly IisPreCondition _preConditionOutcome;

    internal IisRegistration(
        string name,
        Dictionary<string, string> attributes,
        IisPreCondition preConditionOutcome)
    {
        Name = name;
        _attributes = attributes;
        _preConditionOutcome = preConditionOutcome;
    }

    internal string Name { get; }

    internal bool PreConditionSatisfied => _preConditionOutcome.Satisfied;

    // Per-request, not decided here: the pipeline runs the entry only for a managed handler
    // (MH10). The preCondition attribute text stays verbatim even when this reads false.
    internal bool RequiresManagedHandler => _preConditionOutcome.RequiresManagedHandler;

    internal IisRegistration WithoutManagedHandlerCondition() =>
        RequiresManagedHandler
            ? new IisRegistration(Name, _attributes, _preConditionOutcome.WithoutManagedHandler())
            : this;

    internal string? Type => Attribute("type");

    internal string? PreCondition => Attribute("preCondition");

    internal string? Path => Attribute("path");

    internal string? Verb => Attribute("verb");

    internal string? Modules => Attribute("modules");

    internal string? ResourceType => Attribute("resourceType");

    internal string? Attribute(string name) =>
        _attributes.TryGetValue(name, out var value) ? value : null;
}

internal enum IisRegistrationPlacement
{
    Tail,
    Head,
}

// The measured merge semantics (MH1, MH2, MH8, MH9v, MH13-MH15, MH18-MH20). An inherited name
// keeps its position across a remove and re-add, so removal empties its slot instead of dropping
// it. Names compare with Ordinal: IIS_schema.xml declares the key caseSensitive.
internal sealed class IisRegistrationSection
{
    private static readonly string[] ModuleRequiredAttributes = { "name" };
    private static readonly string[] HandlerRequiredAttributes = { "name", "path", "verb" };

    private const string RunAllManagedModulesAttribute = "runAllManagedModulesForAllRequests";

    private readonly List<Slot> _inherited = new();
    private readonly List<IisRegistration> _fresh = new();
    private readonly string _sectionName;
    private readonly string _entryKind;
    private readonly IisRegistrationPlacement _placement;
    private readonly bool _clearHonored;
    private readonly bool _runAllManagedModulesHonored;
    private readonly string[] _requiredAttributes;
    private bool _inheritanceSealed;
    private bool _runAllManagedModules;

    private IisRegistrationSection(
        string sectionName,
        string entryKind,
        IisRegistrationPlacement placement,
        bool clearHonored,
        bool runAllManagedModulesHonored,
        string[] requiredAttributes)
    {
        _sectionName = sectionName;
        _entryKind = entryKind;
        _placement = placement;
        _clearHonored = clearHonored;
        _runAllManagedModulesHonored = runAllManagedModulesHonored;
        _requiredAttributes = requiredAttributes;
    }

    internal static IisRegistrationSection ForModules() =>
        new(
            "modules",
            "Module",
            IisRegistrationPlacement.Tail,
            clearHonored: false,
            runAllManagedModulesHonored: true,
            ModuleRequiredAttributes);

    internal static IisRegistrationSection ForHandlers() =>
        new(
            "handlers",
            "Handler",
            IisRegistrationPlacement.Head,
            clearHonored: true,
            runAllManagedModulesHonored: false,
            HandlerRequiredAttributes);

    internal bool RunAllManagedModules => _runAllManagedModules;

    internal void SealInheritance()
    {
        _inherited.RemoveAll(slot => slot.Row == null);
        _inheritanceSealed = true;
    }

    internal IReadOnlyList<IisRegistration> Build()
    {
        var effective = new List<IisRegistration>(_inherited.Count + _fresh.Count);

        if (_placement == IisRegistrationPlacement.Head)
        {
            AddEffective(effective, _fresh);
        }

        foreach (var slot in _inherited)
        {
            if (slot.Row != null)
            {
                AddEffective(effective, slot.Row);
            }
        }

        if (_placement == IisRegistrationPlacement.Tail)
        {
            AddEffective(effective, _fresh);
        }

        return effective.ToArray();
    }

    internal void Apply(XmlNode sectionNode, string configPath)
    {
        if (_runAllManagedModulesHonored)
        {
            ApplyRunAllManagedModules(sectionNode, configPath);
        }

        foreach (XmlNode node in sectionNode.ChildNodes)
        {
            if (node.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (node.Name == "clear")
            {
                ApplyClear(configPath);
            }
            else if (node.Name == "remove")
            {
                ApplyRemove(IisCollectionReader.RequireAttribute(node, "name", configPath));
            }
            else if (node.Name == "add")
            {
                ApplyAdd(ReadRow(node, configPath), configPath);
            }
            else
            {
                throw new ConfigurationErrorsException(
                    "'" + configPath + "' contains unsupported element <" + node.Name
                    + "> inside <" + _sectionName + ">.");
            }
        }
    }

    private void AddEffective(List<IisRegistration> effective, List<IisRegistration> rows)
    {
        foreach (var row in rows)
        {
            AddEffective(effective, row);
        }
    }

    private void AddEffective(List<IisRegistration> effective, IisRegistration row)
    {
        if (!row.PreConditionSatisfied)
        {
            return;
        }

        effective.Add(_runAllManagedModules ? row.WithoutManagedHandlerCondition() : row);
    }

    private void ApplyRunAllManagedModules(XmlNode sectionNode, string configPath)
    {
        var value = sectionNode.Attributes?[RunAllManagedModulesAttribute]?.Value;
        if (value == null)
        {
            return;
        }

        if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
        {
            _runAllManagedModules = true;
        }
        else if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
        {
            _runAllManagedModules = false;
        }
        else
        {
            throw new ConfigurationErrorsException(
                "<" + _sectionName + " " + RunAllManagedModulesAttribute + "=\"" + value
                + "\"> in '" + configPath + "' is not a boolean; IIS accepts only \"true\" or"
                + " \"false\".");
        }
    }

    private void ApplyClear(string configPath)
    {
        if (!_clearHonored)
        {
            throw new ConfigurationErrorsException(
                "<clear /> in '" + configPath + "' is not allowed inside <" + _sectionName
                + ">: the inherited entries are locked and IIS refuses this with a 500.19 lock"
                + " violation. <remove> the entries the application does not want instead.");
        }

        _inherited.Clear();
        _fresh.Clear();
    }

    private void ApplyRemove(string name)
    {
        var fresh = _fresh.FindIndex(row => Matches(row.Name, name));
        if (fresh >= 0)
        {
            _fresh.RemoveAt(fresh);
            return;
        }

        var slot = _inherited.Find(candidate => Matches(candidate.Name, name));
        if (slot != null)
        {
            slot.Row = null;
        }
    }

    private void ApplyAdd(IisRegistration row, string configPath)
    {
        if (_fresh.Exists(existing => Matches(existing.Name, row.Name))
            || _inherited.Exists(slot => slot.Row != null && Matches(slot.Name, row.Name)))
        {
            throw new ConfigurationErrorsException(
                "<add name=\"" + row.Name + "\"> in '" + configPath + "' duplicates an entry the <"
                + _sectionName + "> collection already contains; IIS refuses this with a 500.19."
                + " <remove> it first to change it.");
        }

        if (!_inheritanceSealed)
        {
            _inherited.Add(new Slot(row.Name) { Row = row });
            return;
        }

        var inheritedSlot = _inherited.Find(slot => Matches(slot.Name, row.Name));
        if (inheritedSlot != null)
        {
            inheritedSlot.Row = row;
            return;
        }

        _fresh.Add(row);
    }

    private IisRegistration ReadRow(XmlNode node, string configPath)
    {
        foreach (var required in _requiredAttributes)
        {
            IisCollectionReader.RequireAttribute(node, required, configPath);
        }

        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        if (node.Attributes != null)
        {
            foreach (XmlAttribute attribute in node.Attributes)
            {
                attributes[attribute.Name] = attribute.Value;
            }
        }

        var name = attributes["name"];
        attributes.TryGetValue("preCondition", out var preCondition);

        return new IisRegistration(
            name,
            attributes,
            IisPreConditions.Evaluate(_entryKind, name, preCondition, configPath));
    }

    private static bool Matches(string entry, string name) =>
        string.Equals(entry, name, StringComparison.Ordinal);

    private sealed class Slot
    {
        internal Slot(string name)
        {
            Name = name;
        }

        internal string Name { get; }

        internal IisRegistration? Row { get; set; }
    }
}
