namespace Rehost.WebForms.Hosting;

using System;
using System.Collections.Generic;

internal interface IApplicationData
{
    object Get(string key);

    void Set(string key, object value);
}

internal sealed class AppDomainApplicationData : IApplicationData
{
    public object Get(string key)
    {
        return AppDomain.CurrentDomain.GetData(key);
    }

    public void Set(string key, object value)
    {
        AppDomain.CurrentDomain.SetData(key, value);
    }
}

internal static class ApplicationBinding
{
    private static readonly string[] Keys =
    {
        ".appId",
        ".appPath",
        ".appVPath",
        ".domainId",
        ".appDomain",
    };

    internal static void Bind(
        ApplicationBootstrapConfiguration configuration,
        IApplicationData applicationData)
    {
        var previousValues = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var key in Keys)
        {
            previousValues.Add(key, applicationData.Get(key));
        }

        foreach (var pair in previousValues)
        {
            if (pair.Value != null)
            {
                throw new InvalidOperationException(
                    $"The current AppDomain already contains a Web Forms application binding at '{pair.Key}'.");
            }
        }

        try
        {
            applicationData.Set(".appId", configuration.ApplicationId);
            applicationData.Set(".appPath", configuration.PhysicalRootPath);
            applicationData.Set(".appVPath", configuration.VirtualRootPath);
            applicationData.Set(".domainId", configuration.ApplicationId + "-current");
            applicationData.Set(".appDomain", "*");
        }
        catch
        {
            foreach (var key in Keys)
            {
                applicationData.Set(key, previousValues[key]);
            }

            throw;
        }
    }
}
