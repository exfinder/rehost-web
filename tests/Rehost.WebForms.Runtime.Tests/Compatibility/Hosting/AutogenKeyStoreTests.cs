using Rehost.WebForms.Hosting;
using Shouldly;
using System.Configuration;
using System.Web.Configuration;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Hosting;

[Collection(nameof(ApplicationBootstrapCollection))]
public sealed class AutogenKeyStoreTests
{
    [Fact]
    public void Preflight_Creates_A_Persisted_Key_File_For_The_Shipped_Default()
    {
        using var application = TemporaryApplication.Create();
        var configuration = application.CreateConfiguration();

        ApplicationConfigurationPreflight.Validate(configuration);

        var path = AutogenKeyStore.ResolveKeyFilePath(configuration, out var source);
        source.ShouldBe(AutogenKeyStore.HostSource);
        File.ReadAllBytes(path).Length.ShouldBe(AutogenKeyStore.KeyFileLength);
    }

    [Fact]
    public void The_Keys_Come_From_The_File_And_Survive_A_Second_Load()
    {
        using var application = TemporaryApplication.Create();
        var configuration = application.CreateConfiguration();
        var path = AutogenKeyStore.ResolveKeyFilePath(configuration, out _);

        var first = AutogenKeyStore.LoadOrCreate(configuration);

        File.ReadAllBytes(path).ShouldBe(first);
        AutogenKeyStore.LoadOrCreate(configuration).ShouldBe(first);
        File.Delete(path);
        AutogenKeyStore.LoadOrCreate(configuration).ShouldNotBe(first);
    }

    [Fact]
    public void Explicit_Keys_Create_No_Key_File()
    {
        using var application = TemporaryApplication.Create();
        WriteMachineKey(
            application,
            "validationKey=\"E5E96E17E2FC4E4F325AD4B9374112BC5298EA7F6833E5EB2829FA61DEA8ABC7DBF63EDD75EE4A5B5B8E14A71B0F9E375BB35059AD8F42F314B48F1AB9AD337B\" "
                + "decryptionKey=\"5D96763F754B1A37C60A65B3B47C54684BDFE91CB5F56870021D5DA0477F6C3A\"");

        ApplicationConfigurationPreflight.Validate(application.CreateConfiguration());

        Directory.Exists(application.MachineKeyDirectory).ShouldBeFalse();
    }

    [Fact]
    public void A_Key_File_Of_The_Wrong_Length_Fails_Preflight()
    {
        using var application = TemporaryApplication.Create();
        var configuration = application.CreateConfiguration();
        var path = AutogenKeyStore.ResolveKeyFilePath(configuration, out _);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[16]);

        var failure = Should.Throw<InvalidOperationException>(
            () => ApplicationConfigurationPreflight.Validate(configuration));

        failure.Message.ShouldContain(path);
        failure.Message.ShouldContain("16 bytes");
    }

    [Fact]
    public void An_Unwritable_Key_Directory_Fails_Preflight()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Write permission is removed with chmod, which NTFS does not model.");
            return;
        }

        Assert.SkipWhen(
            Environment.IsPrivilegedProcess, "A privileged process ignores the mode bits.");

        using var application = TemporaryApplication.Create();
        var configuration = application.CreateConfiguration();
        Directory.CreateDirectory(application.MachineKeyDirectory);
        File.SetUnixFileMode(
            application.MachineKeyDirectory,
            UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            var failure = Should.Throw<InvalidOperationException>(
                () => ApplicationConfigurationPreflight.Validate(configuration));

            failure.Message.ShouldContain(AutogenKeyStore.HostSource);
        }
        finally
        {
            File.SetUnixFileMode(
                application.MachineKeyDirectory,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Fact]
    public void The_Key_File_Is_Owner_Only_On_Unix()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix file modes do not exist on NTFS.");
            return;
        }

        using var application = TemporaryApplication.Create();
        var configuration = application.CreateConfiguration();

        AutogenKeyStore.LoadOrCreate(configuration);

        var path = AutogenKeyStore.ResolveKeyFilePath(configuration, out _);
        File.GetUnixFileMode(path).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    [Fact]
    public void A_Trailing_Separator_Does_Not_Change_The_Key_File_Identity()
    {
        var root = Path.Combine(Path.GetTempPath(), "rehost-keys-identity");

        AutogenKeyStore.FileSegment(root + Path.DirectorySeparatorChar)
            .ShouldBe(AutogenKeyStore.FileSegment(root));

        if (OperatingSystem.IsWindows())
        {
            AutogenKeyStore.FileSegment(root.ToUpperInvariant())
                .ShouldBe(AutogenKeyStore.FileSegment(root));
        }
    }

    [Fact]
    public void An_Environment_Key_Conflicting_With_An_Explicit_Key_Fails_Preflight()
    {
        using var application = TemporaryApplication.Create();
        WriteMachineKey(
            application,
            "validationKey=\"E5E96E17E2FC4E4F325AD4B9374112BC5298EA7F6833E5EB2829FA61DEA8ABC7DBF63EDD75EE4A5B5B8E14A71B0F9E375BB35059AD8F42F314B48F1AB9AD337B\" "
                + "decryptionKey=\"AutoGenerate,IsolateApps\"");

        var failure = WithEnvironmentKeys(
            validationKey: "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF",
            decryptionKey: null,
            () => Should.Throw<ConfigurationErrorsException>(
                () => ApplicationConfigurationPreflight.Validate(application.CreateConfiguration())));

        failure.Message.ShouldContain(MachineKeyEnvironmentOverrides.ValidationKeyVariable);
    }

    [Fact]
    public void Environment_Keys_Replace_Auto_Generation_And_Create_No_Key_File()
    {
        using var application = TemporaryApplication.Create();

        WithEnvironmentKeys(
            validationKey: "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF",
            decryptionKey: "5D96763F754B1A37C60A65B3B47C54684BDFE91CB5F56870021D5DA0477F6C3A",
            () =>
            {
                ApplicationConfigurationPreflight.Validate(application.CreateConfiguration());
                return 0;
            });

        Directory.Exists(application.MachineKeyDirectory).ShouldBeFalse();
    }

    [Fact]
    public void A_Malformed_Environment_Key_Fails_Preflight()
    {
        using var application = TemporaryApplication.Create();

        var failure = WithEnvironmentKeys(
            validationKey: "not-hex-at-all",
            decryptionKey: null,
            () => Should.Throw<ConfigurationErrorsException>(
                () => ApplicationConfigurationPreflight.Validate(application.CreateConfiguration())));

        failure.Message.ShouldContain(MachineKeyEnvironmentOverrides.ValidationKeyVariable);
    }

    private static T WithEnvironmentKeys<T>(string? validationKey, string? decryptionKey, Func<T> act)
    {
        Environment.SetEnvironmentVariable(
            MachineKeyEnvironmentOverrides.ValidationKeyVariable, validationKey);
        Environment.SetEnvironmentVariable(
            MachineKeyEnvironmentOverrides.DecryptionKeyVariable, decryptionKey);
        try
        {
            return act();
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                MachineKeyEnvironmentOverrides.ValidationKeyVariable, null);
            Environment.SetEnvironmentVariable(
                MachineKeyEnvironmentOverrides.DecryptionKeyVariable, null);
        }
    }

    private static void WriteMachineKey(TemporaryApplication application, string attributes)
    {
        File.WriteAllText(
            Path.Combine(application.PhysicalRoot.FullName, "web.config"),
            $"""
            <configuration>
              <system.web>
                <machineKey {attributes} validation="HMACSHA256" decryption="AES" />
              </system.web>
            </configuration>
            """);
    }
}
