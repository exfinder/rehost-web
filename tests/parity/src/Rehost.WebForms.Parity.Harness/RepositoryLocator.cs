using System;
using System.IO;

namespace Rehost.WebForms.Parity.Harness;

public static class RepositoryLocator
{
    public static string FindRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);

        while (directory != null
            && !File.Exists(Path.Combine(directory.FullName, "Rehost.WebForms.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Repository root was not found.");
    }
}
