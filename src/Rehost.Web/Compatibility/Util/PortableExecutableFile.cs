#nullable enable

using System.IO;
using System.Reflection.PortableExecutable;

namespace System.Web.Util;

internal static class PortableExecutableFile
{
    // True only for a readable PE image carrying a CLI metadata directory; a native library, a
    // truncated or empty file, and a non-PE file named .dll all answer false.
    internal static bool HasManagedMetadata(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var peReader = new PEReader(stream);

            return peReader.HasMetadata;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
