using System.PrivateResources;
using System.Security;
using System.Text;
using System.Xml;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests;

public sealed class ResXResourceReaderTests
{
    private const string ResMimeTypeHeader = """
        <resheader name="resmimetype">
          <value>text/microsoft-resx</value>
        </resheader>
        """;

    [Fact]
    public void Reads_String_Value()
    {
        var value = ReadSingleValue(Data("message", "hello"));

        value.ShouldBe("hello");
    }

    [Fact]
    public void Preserves_Significant_String_Whitespace()
    {
        var value = ReadSingleValue("""
            <data name="message" xml:space="preserve">
              <value>  hello  </value>
            </data>
            """);

        value.ShouldBe("  hello  ");
    }

    [Fact]
    public void Converts_Primitive_Value_From_Type_Name()
    {
        var value = ReadSingleValue(Data("answer", "42", typeof(int).AssemblyQualifiedName));

        value.ShouldBe(42);
    }

    [Fact]
    public void Reads_Legacy_Mscorlib_Byte_Array()
    {
        var value = ReadSingleValue(Data("bytes", "AQID", "System.Byte[], mscorlib"));

        value.ShouldBe(new byte[] { 1, 2, 3 });
    }

    [Fact]
    public void Reads_Null_Reference()
    {
        var value = ReadSingleValue(Data("nothing", string.Empty, typeof(ResXNullRef).AssemblyQualifiedName));

        value.ShouldBeNull();
    }

    [Fact]
    public void Duplicate_Name_Uses_Last_Value()
    {
        var value = ReadSingleValue(Data("message", "first") + Data("message", "second"));

        value.ShouldBe("second");
    }

    [Fact]
    public void Assembly_Alias_Resolves_Type()
    {
        var assemblyName = SecurityElement.Escape(typeof(int).Assembly.FullName)!;
        var body = $"""
            <assembly alias="core" name="{assemblyName}" />
            {Data("answer", "42", "System.Int32, core")}
            """;

        ReadSingleValue(body).ShouldBe(42);
    }

    [Fact]
    public void Metadata_Is_Enumerated_Separately()
    {
        using var reader = CreateReader("""
            <metadata name="design-time"><value>metadata</value></metadata>
            <data name="runtime"><value>data</value></data>
            """);

        var metadata = reader.GetMetadataEnumerator();
        metadata.MoveNext().ShouldBeTrue();
        metadata.Key.ShouldBe("design-time");
        metadata.Value.ShouldBe("metadata");
        metadata.MoveNext().ShouldBeFalse();

        var data = reader.GetEnumerator();
        data.MoveNext().ShouldBeTrue();
        data.Key.ShouldBe("runtime");
        data.Value.ShouldBe("data");
        data.MoveNext().ShouldBeFalse();
    }

    [Fact]
    public void Data_Node_Mode_Preserves_Node_Information()
    {
        using var reader = CreateReader("""
            <data name="message">
              <value>hello</value>
              <comment>greeting</comment>
            </data>
            """);
        reader.UseResXDataNodes = true;

        var enumerator = reader.GetEnumerator();
        enumerator.MoveNext().ShouldBeTrue();
        var node = enumerator.Value.ShouldBeOfType<ResXDataNode>();

        node.Name.ShouldBe("message");
        node.Comment.ShouldBe("greeting");
        node.GetNodePosition().Y.ShouldBeGreaterThan(0);
        node.GetValue((System.ComponentModel.Design.ITypeResolutionService?)null).ShouldBe("hello");
    }

    [Fact]
    public void Base_Path_Resolves_File_Reference_Value()
    {
        var directory = Directory.CreateTempSubdirectory("rehost-resx-");
        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "message.txt"), "from file", Encoding.UTF8);
            using var reader = CreateReader(FileReferenceData("message.txt"));
            reader.BasePath = directory.FullName;

            var enumerator = reader.GetEnumerator();
            enumerator.MoveNext().ShouldBeTrue();
            enumerator.Value.ShouldBe("from file");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void Data_Node_File_Reference_Uses_Base_Path()
    {
        var basePath = Path.Combine(Path.GetTempPath(), "resx-base");
        using var reader = CreateReader(FileReferenceData("message.txt"));
        reader.BasePath = basePath;
        reader.UseResXDataNodes = true;

        var enumerator = reader.GetEnumerator();
        enumerator.MoveNext().ShouldBeTrue();
        var node = enumerator.Value.ShouldBeOfType<ResXDataNode>();

        node.FileRef.FileName.ShouldBe(Path.Combine(basePath, "message.txt"));
    }

    [Fact]
    public void Resource_Enumeration_Locks_Reader_Settings()
    {
        using var reader = CreateReader(Data("message", "hello"));
        _ = reader.GetEnumerator();

        Should.Throw<InvalidOperationException>(() => reader.BasePath = "other");
        Should.Throw<InvalidOperationException>(() => reader.UseResXDataNodes = true);
    }

    [Fact]
    public void Metadata_Enumeration_Does_Not_Lock_Reader_Settings()
    {
        using var reader = CreateReader("""<metadata name="value"><value>metadata</value></metadata>""");
        _ = reader.GetMetadataEnumerator();

        reader.BasePath = "other";
        reader.UseResXDataNodes = true;
    }

    [Fact]
    public void Close_Disposes_Text_Reader()
    {
        var textReader = new TrackingTextReader(Document(Data("message", "hello")));
        var reader = new ResXResourceReader(textReader);

        reader.Close();

        textReader.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public void Malformed_Xml_Is_Wrapped_In_Argument_Exception()
    {
        using var reader = ResXResourceReader.FromFileContents("<root>");

        var exception = Should.Throw<ArgumentException>(() => reader.GetEnumerator());

        exception.InnerException.ShouldBeOfType<XmlException>();
    }

    [Fact]
    public void Data_Without_Name_Is_Rejected()
    {
        using var reader = CreateReader("<data><value>hello</value></data>");

        Should.Throw<ArgumentException>(() => reader.GetEnumerator());
    }

    [Fact]
    public void Missing_Mime_Type_Header_Is_Rejected()
    {
        using var reader = ResXResourceReader.FromFileContents(Document(Data("message", "hello"), string.Empty));

        Should.Throw<ArgumentException>(() => reader.GetEnumerator());
    }

    [Fact]
    public void Wrong_Mime_Type_Header_Is_Rejected()
    {
        const string headers = """<resheader name="resmimetype"><value>text/plain</value></resheader>""";
        using var reader = ResXResourceReader.FromFileContents(Document(Data("message", "hello"), headers));

        Should.Throw<ArgumentException>(() => reader.GetEnumerator());
    }

    [Fact]
    public void Header_Names_Are_Case_Insensitive()
    {
        const string headers = """<resheader name="ResMimeType"><value>text/microsoft-resx</value></resheader>""";
        using var reader = ResXResourceReader.FromFileContents(Document(Data("message", "hello"), headers));

        var enumerator = reader.GetEnumerator();

        enumerator.MoveNext().ShouldBeTrue();
        enumerator.Value.ShouldBe("hello");
    }

    [Fact]
    public void System_Web_Profile_Accepts_Mismatched_Reader_Writer_Headers()
    {
        const string headers = """
            <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
            <resheader name="reader"><value>Other.Reader, Other</value></resheader>
            <resheader name="writer"><value>Other.Writer, Other</value></resheader>
            """;
        using var reader = ResXResourceReader.FromFileContents(Document(Data("message", "hello"), headers));

        var enumerator = reader.GetEnumerator();

        enumerator.MoveNext().ShouldBeTrue();
        enumerator.Value.ShouldBe("hello");
    }

    private static ResXResourceReader CreateReader(string body) =>
        ResXResourceReader.FromFileContents(Document(body));

    private static object? ReadSingleValue(string body)
    {
        using var reader = CreateReader(body);
        var enumerator = reader.GetEnumerator();

        enumerator.MoveNext().ShouldBeTrue();
        var value = enumerator.Value;
        enumerator.MoveNext().ShouldBeFalse();
        return value;
    }

    private static string Data(string name, string value, string? typeName = null)
    {
        var typeAttribute = typeName is null
            ? string.Empty
            : $" type=\"{SecurityElement.Escape(typeName)}\"";
        return $"<data name=\"{name}\"{typeAttribute}><value>{value}</value></data>";
    }

    private static string FileReferenceData(string fileName) =>
        Data(
            "file",
            $"{fileName};System.String, mscorlib;utf-8",
            typeof(ResXFileRef).AssemblyQualifiedName);

    private static string Document(string body, string headers = ResMimeTypeHeader) =>
        $"<root>{headers}{body}</root>";

    private sealed class TrackingTextReader(string value) : StringReader(value)
    {
        public bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
