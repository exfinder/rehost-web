using System;
using System.IO;
using System.Linq;
using Rehost.WebForms.Parity.Contracts;

namespace Rehost.WebForms.Parity.Harness;

public static class ManifestLoader
{
    public static SessionManifest Load(string path)
    {
        path = Path.GetFullPath(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Session manifest is absent.", path);
        }

        var manifest = ParityJson.Deserialize<SessionManifest>(File.ReadAllText(path))
            ?? throw new InvalidDataException("Session manifest deserialized to null.");

        if (manifest.SchemaVersion != 1)
        {
            throw new InvalidDataException("Unsupported session manifest schema.");
        }

        if (manifest.Sessions.Count == 0)
        {
            throw new InvalidDataException("Session manifest declares no sessions.");
        }

        return manifest;
    }

    public static SessionSpecification FindSession(SessionManifest manifest, string name)
    {
        return manifest.Sessions.FirstOrDefault(
                candidate => string.Equals(candidate.Name, name, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                "Manifest declares no session named '" + name + "'.");
    }
}
