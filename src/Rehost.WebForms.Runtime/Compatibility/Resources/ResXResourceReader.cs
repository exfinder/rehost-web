// Adapted from dotnet/winforms commit 195f89af79d550c2da1711c45c379efd63519ac1.
// Licensed to the .NET Foundation under one or more agreements under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace System.Resources;

internal sealed class ResXResourceReader : IResourceReader
{
    private const string BinaryMimeType = "application/x-microsoft.net.object.binary.base64";
    private const string CompatibleBinaryMimeType = "text/microsoft-urt/binary-serialized/base64";
    private const string BetaBinaryMimeType = "text/microsoft-urt/psuedoml-serialized/base64";
    private const string SoapMimeType = "application/x-microsoft.net.object.soap.base64";
    private const string CompatibleSoapMimeType = "text/microsoft-urt/soap-serialized/base64";
    private const string ByteArrayMimeType = "application/x-microsoft.net.object.bytearray.base64";

    private readonly Stream _stream;
    private Hashtable _resources;

    internal ResXResourceReader(Stream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    }

    internal string BasePath { get; set; }

    public IDictionaryEnumerator GetEnumerator()
    {
        EnsureResources();
        return _resources.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Close() => Dispose();

    public void Dispose() => _stream.Dispose();

    private void EnsureResources()
    {
        if (_resources != null)
        {
            return;
        }

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };

        XDocument document;
        using (var reader = XmlReader.Create(_stream, settings))
        {
            document = XDocument.Load(reader, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        }

        var aliases = ReadAliases(document);
        var resources = new Hashtable(StringComparer.Ordinal);
        foreach (var element in document.Root?.Elements("data") ?? Array.Empty<XElement>())
        {
            var name = (string)element.Attribute("name");
            if (String.IsNullOrEmpty(name))
            {
                continue;
            }

            var typeName = ExpandAlias((string)element.Attribute("type"), aliases);
            var mimeType = (string)element.Attribute("mimetype");
            var value = element.Element("value")?.Value;
            resources[name] = ReadValue(name, typeName, mimeType, value);
        }

        _resources = resources;
    }

    private static Dictionary<string, string> ReadAliases(XDocument document)
    {
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in document.Root?.Elements("assembly") ?? Array.Empty<XElement>())
        {
            var alias = (string)element.Attribute("alias");
            var name = (string)element.Attribute("name");
            if (!String.IsNullOrEmpty(alias) && !String.IsNullOrEmpty(name))
            {
                aliases[alias] = name;
            }
        }

        return aliases;
    }

    private object ReadValue(string resourceName, string typeName, string mimeType, string value)
    {
        if (!String.IsNullOrEmpty(mimeType))
        {
            if (IsMimeType(mimeType, SoapMimeType, CompatibleSoapMimeType))
            {
                throw Unsupported(resourceName, "SOAP-serialized object");
            }

            if (IsMimeType(mimeType, BinaryMimeType, CompatibleBinaryMimeType, BetaBinaryMimeType))
            {
                return DeserializeBinary(value);
            }

            if (String.Equals(mimeType, ByteArrayMimeType, StringComparison.OrdinalIgnoreCase))
            {
                return ConvertBytes(resourceName, typeName, DecodeBase64(value));
            }

            throw Unsupported(resourceName, $"MIME type '{mimeType}'");
        }

        if (String.IsNullOrEmpty(typeName))
        {
            return value ?? String.Empty;
        }

        if (IsType(typeName, "System.Resources.ResXNullRef"))
        {
            return null;
        }

        if (IsType(typeName, "System.Resources.ResXFileRef"))
        {
            return ReadFileReference(resourceName, value);
        }

        if (IsType(typeName, "System.Byte[]"))
        {
            return DecodeBase64(value);
        }

        var type = ResolveType(resourceName, typeName);
        RejectWindowsGraphics(resourceName, type);
        var converter = TypeDescriptor.GetConverter(type);
        if (!converter.CanConvertFrom(typeof(string)))
        {
            throw Unsupported(resourceName, $"type '{typeName}' without a string converter");
        }

        return converter.ConvertFromInvariantString(value);
    }

    private object ReadFileReference(string resourceName, string value)
    {
        var parts = ParseFileReference(value);
        if (parts.Count < 2)
        {
            throw new InvalidOperationException($"Resource '{resourceName}' has an invalid ResXFileRef value.");
        }

        var path = parts[0];
        if (!Path.IsPathRooted(path))
        {
            if (String.IsNullOrEmpty(BasePath))
            {
                throw new InvalidOperationException($"Resource '{resourceName}' has a relative file reference without a base path.");
            }

            path = Path.Combine(BasePath, path);
        }

        var type = ResolveType(resourceName, parts[1]);
        RejectWindowsGraphics(resourceName, type);
        if (type == typeof(string))
        {
            var encoding = parts.Count > 2 ? Encoding.GetEncoding(parts[2]) : Encoding.Default;
            return File.ReadAllText(path, encoding);
        }

        var bytes = File.ReadAllBytes(path);
        if (type == typeof(byte[]))
        {
            return bytes;
        }

        var stream = new MemoryStream(bytes, writable: false);
        if (type == typeof(MemoryStream))
        {
            return stream;
        }

        try
        {
            return Activator.CreateInstance(
                type,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.CreateInstance,
                binder: null,
                args: new object[] { stream },
                culture: CultureInfo.InvariantCulture);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    private static object ConvertBytes(string resourceName, string typeName, byte[] bytes)
    {
        var type = ResolveType(resourceName, typeName);
        RejectWindowsGraphics(resourceName, type);
        var converter = TypeDescriptor.GetConverter(type);
        if (!converter.CanConvertFrom(typeof(byte[])))
        {
            throw Unsupported(resourceName, $"type '{typeName}' without a byte-array converter");
        }

        return converter.ConvertFrom(bytes);
    }

    private static object DeserializeBinary(string value)
    {
#pragma warning disable SYSLIB0011
        using var stream = new MemoryStream(DecodeBase64(value), writable: false);
        return new BinaryFormatter().Deserialize(stream);
#pragma warning restore SYSLIB0011
    }

    private static Type ResolveType(string resourceName, string typeName) =>
        Type.GetType(typeName, throwOnError: false) ??
        throw new TypeLoadException($"Resource '{resourceName}' references unavailable type '{typeName}'.");

    private static void RejectWindowsGraphics(string resourceName, Type type)
    {
        if (type.FullName == "System.Drawing.Bitmap" || type.FullName == "System.Drawing.Icon")
        {
            throw Unsupported(resourceName, $"Windows graphics type '{type.FullName}'");
        }
    }

    private static string ExpandAlias(string typeName, IReadOnlyDictionary<string, string> aliases)
    {
        if (String.IsNullOrEmpty(typeName))
        {
            return typeName;
        }

        var comma = typeName.IndexOf(',');
        if (comma < 0)
        {
            return typeName;
        }

        var alias = typeName.Substring(comma + 1).Trim();
        return aliases.TryGetValue(alias, out var assemblyName)
            ? typeName.Substring(0, comma + 1) + " " + assemblyName
            : typeName;
    }

    private static List<string> ParseFileReference(string value)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        foreach (var character in value ?? String.Empty)
        {
            if (character == '"')
            {
                quoted = !quoted;
            }
            else if (character == ';' && !quoted)
            {
                parts.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }

        parts.Add(current.ToString().Trim());
        return parts;
    }

    private static byte[] DecodeBase64(string value)
    {
        var compact = new StringBuilder(value?.Length ?? 0);
        foreach (var character in value ?? String.Empty)
        {
            if (!Char.IsWhiteSpace(character))
            {
                compact.Append(character);
            }
        }

        return Convert.FromBase64String(compact.ToString());
    }

    private static bool IsType(string typeName, string expectedName) =>
        typeName.Equals(expectedName, StringComparison.Ordinal) ||
        typeName.StartsWith(expectedName + ",", StringComparison.Ordinal);

    private static bool IsMimeType(string actual, params string[] expected)
    {
        foreach (var value in expected)
        {
            if (String.Equals(actual, value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static PlatformNotSupportedException Unsupported(string resourceName, string format) =>
        new($"Resource '{resourceName}' uses unsupported ResX format {format}.");
}
