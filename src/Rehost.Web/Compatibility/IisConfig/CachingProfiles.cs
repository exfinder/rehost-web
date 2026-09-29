#nullable enable

using System.Configuration;
using System.Xml;

namespace System.Web.IisConfig;

internal enum IisCachePolicy
{
    DontCache,
    CacheUntilChange,
    CacheForTimePeriod,
    DisableCache,
}

internal enum IisCacheLocation
{
    Any,
    Client,
    Downstream,
    Server,
    ServerAndClient,
    None,
}

internal sealed class IisCachingProfile
{
    private IisCachingProfile(
        IisCachePolicy policy, IisCachePolicy kernelCachePolicy, IisCacheLocation location)
    {
        Policy = policy;
        KernelCachePolicy = kernelCachePolicy;
        Location = location;
    }

    internal IisCachePolicy Policy { get; }

    internal IisCachePolicy KernelCachePolicy { get; }

    internal IisCacheLocation Location { get; }

    internal bool StoresUserModeCopy =>
        Policy is IisCachePolicy.CacheUntilChange or IisCachePolicy.CacheForTimePeriod;

    internal string? CacheControl =>
        StoresUserModeCopy
            ? Location switch
            {
                IisCacheLocation.Client or IisCacheLocation.ServerAndClient => "private",
                IisCacheLocation.Any or IisCacheLocation.Downstream => "public",
                _ => "no-cache",
            }
            : null;

    internal static IisCachingProfile Read(XmlNode node, string configPath)
    {
        var extension = IisCollectionReader.RequireAttribute(node, "extension", configPath);

        return new IisCachingProfile(
            ParseEnum<IisCachePolicy>(node, extension, "policy", configPath)
                ?? IisCachePolicy.DontCache,
            ParseEnum<IisCachePolicy>(node, extension, "kernelCachePolicy", configPath)
                ?? IisCachePolicy.DontCache,
            ParseEnum<IisCacheLocation>(node, extension, "location", configPath)
                ?? IisCacheLocation.Server);
    }

    private static TEnum? ParseEnum<TEnum>(
        XmlNode node, string extension, string attribute, string configPath)
        where TEnum : struct, Enum
    {
        var value = node.Attributes?[attribute]?.Value;
        return value == null
            ? null
            : IisCollectionReader.EnumValue<TEnum>(
                $"""<add extension="{extension}" {attribute}="{value}">""", value, configPath);
    }
}
