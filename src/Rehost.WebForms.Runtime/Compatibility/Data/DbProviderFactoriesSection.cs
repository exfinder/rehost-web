#nullable enable

using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;

namespace System.Data.Common;

internal sealed class DbProviderFactoriesSection : ConfigurationSection
{
    internal const string SectionName = "system.data";
    private const string SqlClientInvariantName = "System.Data.SqlClient";

    private static readonly ConfigurationProperty s_factories = new(
        "DbProviderFactories",
        typeof(DbProviderFactoryElementCollection),
        null,
        ConfigurationPropertyOptions.None);
    private static readonly ConfigurationPropertyCollection s_properties = new() { s_factories };

    protected override ConfigurationPropertyCollection Properties => s_properties;

    private DbProviderFactoryElementCollection Factories => (DbProviderFactoryElementCollection)base[s_factories];

    internal static void Register(System.Configuration.Configuration configuration)
    {
        var section = (DbProviderFactoriesSection)configuration.GetSection(SectionName);
        var taken = new HashSet<string>(DbProviderFactories.GetProviderInvariantNames(), StringComparer.Ordinal);
        if (taken.Add(SqlClientInvariantName))
        {
            DbProviderFactories.RegisterFactory(SqlClientInvariantName, SqlClientFactory.Instance);
        }

        foreach (DbProviderFactoryElement factory in section.Factories)
        {
            if (!taken.Contains(factory.Invariant))
            {
                DbProviderFactories.RegisterFactory(factory.Invariant, factory.Type);
            }
        }
    }
}

internal sealed class DbProviderFactoryElementCollection : ConfigurationElementCollection
{
    protected override ConfigurationElement CreateNewElement() => new DbProviderFactoryElement();

    protected override object GetElementKey(ConfigurationElement element) =>
        ((DbProviderFactoryElement)element).Invariant;
}

internal sealed class DbProviderFactoryElement : ConfigurationElement
{
    private static readonly ConfigurationProperty s_name = Required("name");
    private static readonly ConfigurationProperty s_description = Required("description");
    private static readonly ConfigurationProperty s_invariant = Required("invariant", ConfigurationPropertyOptions.IsKey);
    private static readonly ConfigurationProperty s_type = Required("type");
    private static readonly ConfigurationProperty s_support = new("support", typeof(string));
    private static readonly ConfigurationPropertyCollection s_properties =
        new() { s_name, s_description, s_invariant, s_type, s_support };

    protected override ConfigurationPropertyCollection Properties => s_properties;

    internal string Invariant => (string)base[s_invariant];

    internal string Type => (string)base[s_type];

    protected override void PostDeserialize()
    {
        foreach (PropertyInformation property in ElementInformation.Properties)
        {
            if (property.ValueOrigin == PropertyValueOrigin.SetHere && property.Value is "")
            {
                throw new ConfigurationErrorsException(
                    $"Required attribute '{property.Name}' cannot be empty.",
                    property.Source,
                    property.LineNumber);
            }
        }
    }

    private static ConfigurationProperty Required(
        string name,
        ConfigurationPropertyOptions options = ConfigurationPropertyOptions.None) =>
        new(name, typeof(string), null, ConfigurationPropertyOptions.IsRequired | options);
}
