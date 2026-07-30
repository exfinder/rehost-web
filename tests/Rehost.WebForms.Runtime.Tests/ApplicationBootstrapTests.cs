using System.Configuration;
using Rehost.WebForms.Hosting;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests;

[Collection(nameof(ApplicationBootstrapCollection))]
public sealed class ApplicationBootstrapTests
{
    [Fact]
    public void Public_Initialization_Preflights_And_Binds_Process_Application()
    {
        using var application = TemporaryApplication.Create();

        WebFormsApplication.Initialize(application.CreateOptions());

        AppDomain.CurrentDomain.GetData(".appId").ShouldBe("test-app");
        AppDomain.CurrentDomain.GetData(".appPath").ShouldBe(
            application.PhysicalRoot.FullName + Path.DirectorySeparatorChar);
        AppDomain.CurrentDomain.GetData(".appVPath").ShouldBe("/");
        AppDomain.CurrentDomain.GetData(".appDomain").ShouldBe("*");
    }

    [Fact]
    public void Normalizes_Identity_And_Roots_And_Selects_Default_Baselines()
    {
        using var application = TemporaryApplication.Create();
        var configuration = ApplicationBootstrapConfiguration.Create(
            application.CreateOptions("/legacy/"),
            application.OutputDirectory);

        configuration.ApplicationId.ShouldBe("test-app");
        configuration.PhysicalRootPath.ShouldBe(
            application.PhysicalRoot.FullName + Path.DirectorySeparatorChar);
        configuration.VirtualRootPath.ShouldBe("/legacy");
        configuration.MachineConfigurationFilePath.ShouldBe(
            Path.Combine(
                application.OutputDirectory,
                "configs",
                "rehost-webforms.machine.config"));
        configuration.RootWebConfigurationFilePath.ShouldBe(
            Path.Combine(
                application.OutputDirectory,
                "configs",
                "rehost-webforms.web.config"));
    }

    [Fact]
    public void Config_Map_Uses_Explicit_Baselines_And_Application_Root()
    {
        using var application = TemporaryApplication.Create();
        var options = application.CreateOptions("/legacy");
        options.MachineConfigurationFilePath = Path.Combine(
            application.OutputDirectory,
            "configs",
            "rehost-webforms.machine.config");
        options.RootWebConfigurationFilePath = Path.Combine(
            application.OutputDirectory,
            "configs",
            "rehost-webforms.web.config");
        var configuration = ApplicationBootstrapConfiguration.Create(
            options,
            application.OutputDirectory);

        var map = configuration.ConfigMapPathFactory.Create(
            configuration.VirtualRootPath,
            configuration.PhysicalRootPath);
        map.GetPathConfigFilename(
            "1",
            configuration.VirtualRootPath,
            out var directory,
            out var baseName);

        map.GetMachineConfigFilename().ShouldBe(configuration.MachineConfigurationFilePath);
        map.GetRootWebConfigFilename().ShouldBe(configuration.RootWebConfigurationFilePath);
        directory.ShouldBe(Path.TrimEndingDirectorySeparator(configuration.PhysicalRootPath));
        baseName.ShouldBe("web.config");
    }

    [Fact]
    public void Preflight_Accepts_Shipped_Baselines_Without_Application_Config()
    {
        using var application = TemporaryApplication.Create();
        var configuration = application.CreateConfiguration();

        Should.NotThrow(() => ApplicationConfigurationPreflight.Validate(configuration));
    }

    [Fact]
    public void Preflight_Reports_Malformed_Application_Config()
    {
        using var application = TemporaryApplication.Create();
        File.WriteAllText(
            Path.Combine(application.PhysicalRoot.FullName, "web.config"),
            "<configuration><system.web>");
        var configuration = application.CreateConfiguration();

        var exception = Should.Throw<ConfigurationErrorsException>(
            () => ApplicationConfigurationPreflight.Validate(configuration));

        exception.Message.ShouldContain("Web Forms configuration preflight failed");
        exception.Message.ShouldContain("web.config");
        exception.InnerException.ShouldNotBeNull();
    }

    [Fact]
    public void Preflight_Reports_Missing_Explicit_Baseline()
    {
        using var application = TemporaryApplication.Create();
        var options = application.CreateOptions();
        options.MachineConfigurationFilePath = Path.Combine(
            application.PhysicalRoot.FullName,
            "missing.machine.config");
        var configuration = ApplicationBootstrapConfiguration.Create(
            options,
            application.OutputDirectory);

        var exception = Should.Throw<FileNotFoundException>(
            () => ApplicationConfigurationPreflight.Validate(configuration));

        exception.Message.ShouldContain("machine configuration");
        exception.Message.ShouldContain("missing.machine.config");
    }

    [Fact]
    public void Preflight_Reports_Inaccessible_Baseline()
    {
        using var application = TemporaryApplication.Create();
        var configuration = application.CreateConfiguration();
        using var lockedFile = new FileStream(
            configuration.MachineConfigurationFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.None);

        var exception = Should.Throw<IOException>(
            () => ApplicationConfigurationPreflight.Validate(configuration));

        exception.Message.ShouldContain("machine configuration");
        exception.Message.ShouldContain("cannot be read");
    }

    [Fact]
    public void Normalizes_The_Compilation_Temp_Directory_And_Defaults_It_To_Absent()
    {
        using var application = TemporaryApplication.Create();
        var options = application.CreateOptions();

        ApplicationBootstrapConfiguration.Create(options, application.OutputDirectory)
            .CompilationTempDirectory.ShouldBeNull();

        options.CompilationTempDirectory =
            Path.Combine(application.PhysicalRoot.FullName, "codegen") +
            Path.DirectorySeparatorChar;

        ApplicationBootstrapConfiguration.Create(options, application.OutputDirectory)
            .CompilationTempDirectory.ShouldBe(
                Path.Combine(application.PhysicalRoot.FullName, "codegen"));
    }

    [Fact]
    public void Rejects_A_Relative_Compilation_Temp_Directory()
    {
        using var application = TemporaryApplication.Create();
        var options = application.CreateOptions();
        options.CompilationTempDirectory = "codegen";

        var exception = Should.Throw<ArgumentException>(
            () => ApplicationBootstrapConfiguration.Create(options, application.OutputDirectory));

        exception.ParamName.ShouldBe(nameof(WebFormsApplicationOptions.CompilationTempDirectory));
        exception.Message.ShouldContain("must be absolute");
    }

    [Fact]
    public void Rejects_A_Compilation_Temp_Directory_That_Is_A_File()
    {
        using var application = TemporaryApplication.Create();
        var path = Path.Combine(application.PhysicalRoot.FullName, "codegen");
        File.WriteAllText(path, string.Empty);
        var options = application.CreateOptions();
        options.CompilationTempDirectory = path;

        var exception = Should.Throw<ArgumentException>(
            () => ApplicationBootstrapConfiguration.Create(options, application.OutputDirectory));

        exception.Message.ShouldContain("is a file, not a directory");
    }

    [Fact]
    public void Preflight_Rejects_A_Configured_Temp_Directory_That_Disagrees_With_The_Host()
    {
        using var application = TemporaryApplication.Create();
        var configured = Path.Combine(application.PhysicalRoot.FullName, "configured");
        File.WriteAllText(
            Path.Combine(application.PhysicalRoot.FullName, "web.config"),
            $"""
            <configuration>
              <system.web>
                <compilation tempDirectory="{configured}" />
              </system.web>
            </configuration>
            """);
        var options = application.CreateOptions();
        options.CompilationTempDirectory = Path.Combine(application.PhysicalRoot.FullName, "host");
        var configuration = ApplicationBootstrapConfiguration.Create(
            options,
            application.OutputDirectory);

        var exception = Should.Throw<InvalidOperationException>(
            () => ApplicationConfigurationPreflight.Validate(configuration));

        exception.Message.ShouldContain("conflicts with the host");
        exception.Message.ShouldContain(configured);
        exception.Message.ShouldContain(configuration.CompilationTempDirectory);
    }

    [Fact]
    public void Preflight_Accepts_A_Configured_Temp_Directory_That_Agrees_With_The_Host()
    {
        using var application = TemporaryApplication.Create();
        var shared = Path.Combine(application.PhysicalRoot.FullName, "codegen");
        File.WriteAllText(
            Path.Combine(application.PhysicalRoot.FullName, "web.config"),
            $"""
            <configuration>
              <system.web>
                <compilation tempDirectory="{shared + Path.DirectorySeparatorChar}" />
              </system.web>
            </configuration>
            """);
        var options = application.CreateOptions();
        options.CompilationTempDirectory = shared;
        var configuration = ApplicationBootstrapConfiguration.Create(
            options,
            application.OutputDirectory);

        Should.NotThrow(() => ApplicationConfigurationPreflight.Validate(configuration));
    }

    [Fact]
    public void Preflight_Rejects_Configuration_Reload()
    {
        using var application = TemporaryApplication.Create();
        File.WriteAllText(
            Path.Combine(application.PhysicalRoot.FullName, "web.config"),
            """
            <configuration>
              <system.web>
                <httpRuntime fcnMode="Default" />
              </system.web>
            </configuration>
            """);
        var configuration = application.CreateConfiguration();

        var exception = Should.Throw<ConfigurationErrorsException>(
            () => ApplicationConfigurationPreflight.Validate(configuration));

        exception.Message.ShouldContain("Configuration reload is unavailable");
        exception.Message.ShouldContain("fcnMode=\"Disabled\"");
    }

    [Fact]
    public void Successful_Initialization_Publishes_Immutable_Configuration()
    {
        using var application = TemporaryApplication.Create();
        var environment = new RecordingEnvironment(application.OutputDirectory);
        var bootstrap = new ApplicationBootstrap(environment);

        bootstrap.Initialize(application.CreateOptions());

        bootstrap.State.ShouldBe(ApplicationBootstrapState.Initialized);
        bootstrap.Configuration.ShouldNotBeNull();
        environment.PreflightCount.ShouldBe(1);
        environment.BindCount.ShouldBe(1);
    }

    [Fact]
    public void Repeat_Initialization_Fails_Even_When_Options_Match()
    {
        using var application = TemporaryApplication.Create();
        var bootstrap = new ApplicationBootstrap(
            new RecordingEnvironment(application.OutputDirectory));
        var options = application.CreateOptions();
        bootstrap.Initialize(options);

        var exception = Should.Throw<InvalidOperationException>(
            () => bootstrap.Initialize(options));

        exception.Message.ShouldContain("exactly once");
    }

    [Fact]
    public void Conflicting_Initialization_Fails_With_Same_Single_Call_Contract()
    {
        using var application = TemporaryApplication.Create();
        var bootstrap = new ApplicationBootstrap(
            new RecordingEnvironment(application.OutputDirectory));
        bootstrap.Initialize(application.CreateOptions());
        var conflictingOptions = application.CreateOptions();
        conflictingOptions.ApplicationId = "other-app";

        var exception = Should.Throw<InvalidOperationException>(
            () => bootstrap.Initialize(conflictingOptions));

        exception.Message.ShouldContain("exactly once");
    }

    [Fact]
    public async Task Concurrent_Initialization_Has_One_Winner_And_Fails_Other_Call()
    {
        using var application = TemporaryApplication.Create();
        using var preflightStarted = new ManualResetEventSlim();
        using var continuePreflight = new ManualResetEventSlim();
        var cancellationToken = TestContext.Current.CancellationToken;
        var environment = new RecordingEnvironment(
            application.OutputDirectory,
            () =>
            {
                preflightStarted.Set();
                continuePreflight.Wait(cancellationToken);
            });
        var bootstrap = new ApplicationBootstrap(environment);
        var winner = Task.Run(
            () => bootstrap.Initialize(application.CreateOptions()),
            cancellationToken);
        preflightStarted.Wait(cancellationToken);

        var exception = Should.Throw<InvalidOperationException>(
            () => bootstrap.Initialize(application.CreateOptions()));
        continuePreflight.Set();
        await winner;

        exception.Message.ShouldContain("exactly once");
        bootstrap.State.ShouldBe(ApplicationBootstrapState.Initialized);
        environment.BindCount.ShouldBe(1);
    }

    [Fact]
    public void Initialization_Failure_Is_Terminal_And_Does_Not_Publish_Configuration()
    {
        using var application = TemporaryApplication.Create();
        var environment = new RecordingEnvironment(
            application.OutputDirectory,
            preflight: () => throw new ConfigurationErrorsException("broken"));
        var bootstrap = new ApplicationBootstrap(environment);

        Should.Throw<ConfigurationErrorsException>(
            () => bootstrap.Initialize(application.CreateOptions()));

        bootstrap.State.ShouldBe(ApplicationBootstrapState.Faulted);
        bootstrap.Configuration.ShouldBeNull();
        environment.BindCount.ShouldBe(0);
        Should.Throw<InvalidOperationException>(
            () => bootstrap.Initialize(application.CreateOptions()));
    }

    [Fact]
    public void Binding_Failure_Restores_Every_Legacy_Appdomain_Value()
    {
        using var application = TemporaryApplication.Create();
        var configuration = application.CreateConfiguration();
        var data = new ThrowingApplicationData(".domainId");

        Should.Throw<IOException>(() => ApplicationBinding.Bind(configuration, data));

        data.Values[".appId"].ShouldBeNull();
        data.Values[".appPath"].ShouldBeNull();
        data.Values[".appVPath"].ShouldBeNull();
        data.Values[".domainId"].ShouldBeNull();
        data.Values[".appDomain"].ShouldBeNull();
    }

    [Fact]
    public void Binding_Rejects_Preexisting_Appdomain_Identity_Without_Mutation()
    {
        using var application = TemporaryApplication.Create();
        var configuration = application.CreateConfiguration();
        var data = new ThrowingApplicationData(throwOnKey: null);
        data.Values[".appId"] = "other-app";

        var exception = Should.Throw<InvalidOperationException>(
            () => ApplicationBinding.Bind(configuration, data));

        exception.Message.ShouldContain(".appId");
        data.Values[".appId"].ShouldBe("other-app");
        data.Get(".appPath").ShouldBeNull();
    }

    private sealed class RecordingEnvironment : IApplicationBootstrapEnvironment
    {
        private readonly Action _preflight;

        internal RecordingEnvironment(string baseDirectory, Action? preflight = null)
        {
            BaseDirectory = baseDirectory;
            _preflight = preflight ?? (() => { });
        }

        public string BaseDirectory { get; }

        internal int PreflightCount { get; private set; }

        internal int BindCount { get; private set; }

        public void Preflight(ApplicationBootstrapConfiguration configuration)
        {
            PreflightCount++;
            _preflight();
        }

        public void Bind(ApplicationBootstrapConfiguration configuration)
        {
            BindCount++;
        }
    }

    private sealed class ThrowingApplicationData : IApplicationData
    {
        private readonly string? _throwOnKey;
        private bool _hasThrown;

        internal ThrowingApplicationData(string? throwOnKey)
        {
            _throwOnKey = throwOnKey;
        }

        internal Dictionary<string, object?> Values { get; } =
            new(StringComparer.Ordinal);

        public object? Get(string key)
        {
            Values.TryGetValue(key, out var value);
            return value;
        }

        public void Set(string key, object? value)
        {
            if (!_hasThrown && key == _throwOnKey)
            {
                _hasThrown = true;
                throw new IOException("Injected binding failure.");
            }

            Values[key] = value;
        }
    }

    private sealed class TemporaryApplication : IDisposable
    {
        private TemporaryApplication(DirectoryInfo physicalRoot, string outputDirectory)
        {
            PhysicalRoot = physicalRoot;
            OutputDirectory = outputDirectory;
        }

        internal DirectoryInfo PhysicalRoot { get; }

        internal string OutputDirectory { get; }

        internal static TemporaryApplication Create()
        {
            return new TemporaryApplication(
                Directory.CreateTempSubdirectory("rehost-bootstrap-"),
                AppContext.BaseDirectory);
        }

        internal WebFormsApplicationOptions CreateOptions(string virtualRoot = "/")
        {
            return new WebFormsApplicationOptions
            {
                ApplicationId = "test-app",
                PhysicalRootPath = PhysicalRoot.FullName,
                VirtualRootPath = virtualRoot,
            };
        }

        internal ApplicationBootstrapConfiguration CreateConfiguration()
        {
            return ApplicationBootstrapConfiguration.Create(CreateOptions(), OutputDirectory);
        }

        public void Dispose()
        {
            PhysicalRoot.Delete(recursive: true);
        }
    }
}

[CollectionDefinition(nameof(ApplicationBootstrapCollection), DisableParallelization = true)]
public sealed class ApplicationBootstrapCollection;
