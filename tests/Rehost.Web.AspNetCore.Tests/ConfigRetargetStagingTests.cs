using System.Diagnostics;
using System.Text;
using Rehost.Web.TestSupport;
using Shouldly;
using Xunit;

namespace Rehost.Web.AspNetCore.Tests;

public sealed class ConfigRetargetStagingTests : IDisposable
{
    private const string ViewsConfig = """
        <?xml version="1.0"?>
        <!-- <host factoryType="System.Web.Mvc.MvcWebRazorHostFactory, System.Web.Mvc" /> -->
        <configuration>
          <configSections>
            <sectionGroup name="system.web.webPages.razor" type="System.Web.WebPages.Razor.Configuration.RazorWebSectionGroup, System.Web.WebPages.Razor, Version=3.0.0.0, Culture=neutral, PublicKeyToken=31BF3856AD364E35">
              <section name="host" type="System.Web.WebPages.Razor.Configuration.HostSection, System.Web.WebPages.Razor, Version=3.0.0.0, Culture=neutral, PublicKeyToken=31BF3856AD364E35" requirePermission="false" />
            </sectionGroup>
          </configSections>
          <system.web.webPages.razor>
            <host factoryType="System.Web.Mvc.MvcWebRazorHostFactory, System.Web.Mvc, Version=5.2.3.0, Culture=neutral, PublicKeyToken=31BF3856AD364E35" />
            <pages pageBaseType="System.Web.Mvc.WebViewPage">
              <namespaces>
                <!-- <add namespace="System.Web.Optimization, System.Web.Optimization" /> -->
                <add namespace="System.Web.Mvc" />
              </namespaces>
            </pages>
          </system.web.webPages.razor>
          <runtime>
            <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
              <dependentAssembly>
                <assemblyIdentity name="System.Web.Mvc" publicKeyToken="31bf3856ad364e35" />
              </dependentAssembly>
            </assemblyBinding>
          </runtime>
        </configuration>

        """;

    private readonly TempDirectory _root = new("rehost-config-retarget-");

    [Fact]
    public void A_Nested_Config_Keeps_Every_Byte_But_The_Retargeted_Values()
    {
        WriteSite();

        var output = RunStaging();

        File.ReadAllBytes(Staged("Views", "Web.config")).ShouldBe(Utf8WithBom("""
            <?xml version="1.0"?>
            <!-- <host factoryType="System.Web.Mvc.MvcWebRazorHostFactory, System.Web.Mvc" /> -->
            <configuration>
              <configSections>
                <sectionGroup name="system.web.webPages.razor" type="System.Web.WebPages.Razor.Configuration.RazorWebSectionGroup, Rehost.Web.WebPages.Razor">
                  <section name="host" type="System.Web.WebPages.Razor.Configuration.HostSection, Rehost.Web.WebPages.Razor" requirePermission="false" />
                </sectionGroup>
              </configSections>
              <system.web.webPages.razor>
                <host factoryType="System.Web.Mvc.MvcWebRazorHostFactory, Rehost.Web.Mvc" />
                <pages pageBaseType="System.Web.Mvc.WebViewPage">
                  <namespaces>
                    <!-- <add namespace="System.Web.Optimization, System.Web.Optimization" /> -->
                    <add namespace="System.Web.Mvc" />
                  </namespaces>
                </pages>
              </system.web.webPages.razor>
              <runtime>
                <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
                  <dependentAssembly>
                    <assemblyIdentity name="System.Web.Mvc" publicKeyToken="31bf3856ad364e35" />
                  </dependentAssembly>
                </assemblyBinding>
              </runtime>
            </configuration>

            """));
        output.ShouldContain(
            $"{Path.Combine("Views", "Web.config")}(10,11): message : Retargeted"
            + " 'System.Web.Mvc.MvcWebRazorHostFactory, System.Web.Mvc, Version=5.2.3.0,"
            + " Culture=neutral, PublicKeyToken=31BF3856AD364E35'"
            + " to 'System.Web.Mvc.MvcWebRazorHostFactory, Rehost.Web.Mvc'.",
            Case.Sensitive);
    }

    [Fact]
    public void A_Lower_Case_Area_Config_Is_Retargeted_Across_Crlf_Lines_And_Single_Quotes()
    {
        WriteSite();

        RunStaging();

        File.ReadAllText(Staged("Areas", "Admin", "Views", "web.config")).ShouldBe("""
            <?xml version='1.0'?>
            <configuration>
              <system.web>
                <pages
                  pageParserFilterType='System.Web.Mvc.ViewTypeParserFilter, Rehost.Web.Mvc'
                  userControlBaseType='System.Web.Mvc.ViewUserControl, Rehost.Web.Mvc' />
              </system.web>
            </configuration>

            """.ReplaceLineEndings("\r\n"));
    }

    [Fact]
    public void The_Root_Config_Is_Retargeted_After_Its_Transform_And_Stays_So_On_Restaging()
    {
        WriteSite();
        RunStaging();

        RunStaging();

        var root = File.ReadAllText(Staged("web.config"));
        root.ShouldContain("""<add assembly="Rehost.Web.Mvc" />""", Case.Sensitive);
        root.ShouldContain("""<add assembly="*" />""", Case.Sensitive);
        root.ShouldContain(
            """<add assembly="Rehost.AspNet.Web.Optimization.WebForms" namespace="Microsoft.AspNet.Web.Optimization.WebForms" tagPrefix="webopt" />""",
            Case.Sensitive);
        root.ShouldContain(
            """<add name="Mvc" type="Contoso.Wrapper`1[[Contoso.Item, System.Web.Mvc]], Rehost.Web.Mvc" />""",
            Case.Sensitive);
        root.ShouldNotContain("<dependentAssembly");
    }

    private void WriteSite()
    {
        Write(Path.Combine("site", "Web.config"), """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <system.web>
                <compilation>
                  <assemblies>
                    <add assembly="System.Web.Mvc, Version=5.2.3.0, Culture=neutral, PublicKeyToken=31BF3856AD364E35" />
                    <add assembly="*" />
                  </assemblies>
                </compilation>
                <pages>
                  <controls>
                    <add assembly="Microsoft.AspNet.Web.Optimization.WebForms" namespace="Microsoft.AspNet.Web.Optimization.WebForms" tagPrefix="webopt" />
                  </controls>
                </pages>
                <httpModules>
                  <add name="Mvc" type="Contoso.Wrapper`1[[Contoso.Item, System.Web.Mvc]], System.Web.Mvc, Version=5.2.3.0" />
                </httpModules>
              </system.web>
              <runtime>
                <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
                  <dependentAssembly>
                    <assemblyIdentity name="System.Web.Mvc" publicKeyToken="31bf3856ad364e35" />
                  </dependentAssembly>
                </assemblyBinding>
              </runtime>
            </configuration>
            """);
        File.WriteAllBytes(Site("Views", "Web.config"), Utf8WithBom(ViewsConfig));
        Write(Path.Combine("site", "Areas", "Admin", "Views", "web.config"), """
            <?xml version='1.0'?>
            <configuration>
              <system.web>
                <pages
                  pageParserFilterType='System.Web.Mvc.ViewTypeParserFilter, System.Web.Mvc, Version=5.2.3.0, Culture=neutral, PublicKeyToken=31BF3856AD364E35'
                  userControlBaseType='System.Web.Mvc.ViewUserControl, System.Web.Mvc, Version=5.2.3.0, Culture=neutral, PublicKeyToken=31BF3856AD364E35' />
              </system.web>
            </configuration>

            """.ReplaceLineEndings("\r\n"));

        var build = Directory.CreateDirectory(_root.Path("build")).FullName;
        var hostingBuild = Path.Combine(
            RepositoryLocator.FindRoot(AppContext.BaseDirectory), "src", "Rehost.Web.AspNetCore", "build");
        foreach (var file in Directory.GetFiles(hostingBuild))
        {
            File.Copy(file, Path.Combine(build, Path.GetFileName(file)));
        }

        var tasks = Directory.CreateDirectory(Path.Combine(build, "tasks")).FullName;
        var taskOutput = TestOutputPaths.ProjectOutput("eng/Rehost.Web.Build.Tasks");
        foreach (var file in new[] { "Rehost.Web.Build.Tasks.dll", "Microsoft.Web.XmlTransform.dll" })
        {
            File.Copy(Path.Combine(taskOutput, file), Path.Combine(tasks, file));
        }

        Write("site.proj", """
            <Project>
              <Import Project="build/Rehost.Web.AspNetCore.props" />
              <PropertyGroup>
                <RehostSiteContentRoot>site/</RehostSiteContentRoot>
                <BaseIntermediateOutputPath>obj/</BaseIntermediateOutputPath>
                <IntermediateOutputPath>obj/</IntermediateOutputPath>
              </PropertyGroup>
              <Import Project="build/Rehost.Web.AspNetCore.targets" />
            </Project>
            """);
        Directory.CreateDirectory(_root.Path("obj"));
    }

    private string RunStaging()
    {
        var startInfo = new ProcessStartInfo(DotnetPath())
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("msbuild");
        startInfo.ArgumentList.Add(_root.Path("site.proj"));
        startInfo.ArgumentList.Add("-t:RehostStageCore");
        startInfo.ArgumentList.Add("-p:_RehostStageInvocation=build");
        startInfo.ArgumentList.Add("-p:RehostStageRoot=" + _root.Path("stage") + Path.DirectorySeparatorChar);
        startInfo.ArgumentList.Add("-v:n");
        startInfo.ArgumentList.Add("-nr:false");

        using var msbuild = new ScenarioHostProcess(Process.Start(startInfo)!);
        msbuild.WaitForExit();
        msbuild.ExitCode.ShouldBe(0, msbuild.StandardOutput + msbuild.StandardError);
        return msbuild.StandardOutput;
    }

    // PATH's dotnet can lack the SDK global.json pins; the muxer that launched this test run is
    // the one that has it.
    private static string DotnetPath() =>
        Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet"
            ? Environment.ProcessPath!
            : Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";

    private static byte[] Utf8WithBom(string text) =>
        [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text)];

    private void Write(string relative, string content)
    {
        var path = _root.Path(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private string Site(params string[] parts)
    {
        var path = Path.Combine([_root.Path("site"), .. parts]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return path;
    }

    private string Staged(params string[] parts) => Path.Combine([_root.Path("stage"), .. parts]);

    public void Dispose() => _root.Dispose();
}
