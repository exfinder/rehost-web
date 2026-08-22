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
        string configPath,
        Dictionary<string, string> attributes,
        IisPreCondition preConditionOutcome)
    {
        Name = name;
        ConfigPath = configPath;
        _attributes = attributes;
        _preConditionOutcome = preConditionOutcome;
    }

    internal string Name { get; }

    internal string ConfigPath { get; }

    internal bool PreConditionSatisfied => _preConditionOutcome.Satisfied;

    // Per-request, not decided here: the pipeline runs the entry only for a managed handler
    // (MH10). The preCondition attribute text stays verbatim even when this reads false.
    internal bool RequiresManagedHandler => _preConditionOutcome.RequiresManagedHandler;

    internal IisRegistration WithoutManagedHandlerCondition() =>
        RequiresManagedHandler
            ? new IisRegistration(
                Name, ConfigPath, _attributes, _preConditionOutcome.WithoutManagedHandler())
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
//
// Fresh adds are kept one list per configuration level - the application root, then a folder
// web.config for each directory below it - because the deeper level's adds are consulted first
// (MH27).
internal sealed class IisRegistrationSection
{
    private static readonly string[] ModuleRequiredAttributes = { "name" };
    private static readonly string[] HandlerRequiredAttributes = { "name", "path", "verb" };

    private const string RunAllManagedModulesAttribute = "runAllManagedModulesForAllRequests";

    private readonly List<Slot> _inherited = new();
    private readonly List<List<IisRegistration>> _levels = new() { new List<IisRegistration>() };
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

    private IisRegistrationSection(IisRegistrationSection source)
        : this(
            source._sectionName,
            source._entryKind,
            source._placement,
            source._clearHonored,
            source._runAllManagedModulesHonored,
            source._requiredAttributes)
    {
        foreach (var slot in source._inherited)
        {
            _inherited.Add(new Slot(slot.Name) { Row = slot.Row });
        }

        _levels.Clear();
        foreach (var level in source._levels)
        {
            _levels.Add(new List<IisRegistration>(level));
        }

        _inheritanceSealed = source._inheritanceSealed;
        _runAllManagedModules = source._runAllManagedModules;
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

    // One more configuration level below this one, holding its own fresh adds and its own view of
    // everything above: a folder's <remove> of a parent add empties the slot for that folder only
    // (MH27).
    internal IisRegistrationSection Nested()
    {
        var nested = new IisRegistrationSection(this);
        nested._levels.Add(new List<IisRegistration>());
        return nested;
    }

    internal IReadOnlyList<IisRegistration> Build()
    {
        var effective = new List<IisRegistration>();

        if (_placement == IisRegistrationPlacement.Head)
        {
            AddLevels(effective, deepestFirst: true);
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
            AddLevels(effective, deepestFirst: false);
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

    private void AddLevels(List<IisRegistration> effective, bool deepestFirst)
    {
        for (var index = 0; index < _levels.Count; index++)
        {
            foreach (var row in _levels[deepestFirst ? _levels.Count - 1 - index : index])
            {
                AddEffective(effective, row);
            }
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
        foreach (var level in _levels)
        {
            level.Clear();
        }
    }

    private void ApplyRemove(string name)
    {
        for (var level = _levels.Count - 1; level >= 0; level--)
        {
            var fresh = _levels[level].FindIndex(row => Matches(row.Name, name));
            if (fresh >= 0)
            {
                _levels[level].RemoveAt(fresh);
                return;
            }
        }

        var slot = _inherited.Find(candidate => Matches(candidate.Name, name));
        if (slot != null)
        {
            slot.Row = null;
        }
    }

    private void ApplyAdd(IisRegistration row, string configPath)
    {
        if (_levels.Exists(level => level.Exists(existing => Matches(existing.Name, row.Name)))
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

        _levels[^1].Add(row);
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
            configPath,
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
