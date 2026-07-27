using System.Web.Configuration;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests;

public sealed class UserMapPathTests
{
    // Framework combined the child part of a virtual path onto the mapped physical
    // directory by replacing '/' with a literal '\' (ledger P25). Off Windows that
    // produces one filename containing backslashes instead of nested directories.
    [Fact]
    public void MapPath_maps_a_nested_virtual_path_to_nested_physical_directories()
    {
        var root = Directory.CreateTempSubdirectory("rehost-usermappath-");
        try
        {
            var machineConfig = Path.Combine(root.FullName, "machine.config");
            File.WriteAllText(machineConfig, "<configuration />");

            var appRoot = Directory.CreateDirectory(Path.Combine(root.FullName, "app"));

            var fileMap = new WebConfigurationFileMap { MachineConfigFilename = machineConfig };
            fileMap.VirtualDirectories.Add("/", new VirtualDirectoryMapping(appRoot.FullName, isAppRoot: true));

            var mapPath = new UserMapPath(fileMap);

            mapPath.MapPath(siteID: null, "/child/grandchild")
                .ShouldBe(Path.Combine(appRoot.FullName, "child", "grandchild"));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}
