using System;
using System.Web;
using BlogEngine.Core.Providers;
using BlogEngine.Core.Data.Services;

namespace BlogEngine.Core.Packaging
{
    /// <summary>
    /// Responsible for install/uninstall operations
    /// </summary>
    public static class Installer
    {
        private static string PackagesRoot
        {
            get
            {
                return HttpContext.Current.Server.MapPath(Utils.ApplicationRelativeWebRoot + "App_Data/packages");
            }
        }

        /// <summary>
        /// Install package
        /// </summary>
        /// <param name="pkgId"></param>
        public static bool InstallPackage(string pkgId)
        {
            try
            {
                // if package already installed - uninstall it
                if (BlogService.InstalledFromGalleryPackages() != null)
                {
                    if(BlogService.InstalledFromGalleryPackages().Find(p => p.PackageId == pkgId) != null)
                    {
                        UninstallPackage(pkgId);
                    }
                }
                    
                var package = NuGetFeed.FindLatest(pkgId)
                    ?? throw new InvalidOperationException($"Package {pkgId} was not found in {BlogConfig.GalleryFeedUrl}");

                // FileSystem.InstallPackage reads content/ and lib/ from this folder.
                NuGetFeed.Extract(package.Identity, System.IO.Path.Combine(PackagesRoot, $"{package.Identity.Id}.{package.Identity.Version}"));

                var iPkg = new InstalledPackage { PackageId = package.Identity.Id, Version = package.Identity.Version.ToString() };
                BlogService.InsertPackage(iPkg);

                var packageFiles = FileSystem.InstallPackage(package);

                BlogService.InsertPackageFiles(packageFiles);

                Blog.CurrentInstance.Cache.Remove(Constants.CacheKey);

                CustomFieldsParser.ClearCache();

                Utils.Log($"Installed package {pkgId} by {Security.CurrentUser.Identity.Name}");
            }
            catch (Exception ex)
            {
                Utils.Log("BlogEngine.Core.Packaging.Installer.InstallPackage(" + pkgId + ")", ex);
                UninstallPackage(pkgId);
                throw;
            }

            return true;
        }

        /// <summary>
        /// Uninstall package
        /// </summary>
        /// <param name="pkgId"></param>
        /// <returns></returns>
        public static bool UninstallPackage(string pkgId)
        {
            try
            {
                FileSystem.UninstallPackage(pkgId);
                // Utils.Log(string.Format("Uninstalled package {0}: installed package files removed. ", pkgId));

                // remove packagefiles.xml and packages.xml (or DB records)
                BlogService.DeletePackage(pkgId);
                // Utils.Log(string.Format("Uninstalled package {0}: package records removed. ", pkgId));

                UninstallGalleryPackage(pkgId);
                // Utils.Log(string.Format("Uninstalled package {0}: NuGet file removed. ", pkgId));

                // reset cache
                Blog.CurrentInstance.Cache.Remove(Constants.CacheKey);

                Utils.Log($"Uninstalled package {pkgId} by {Security.CurrentUser.Identity.Name}");
            }
            catch (Exception ex)
            {
                Utils.Log($"Error unistalling package {pkgId}: {ex.Message}");
                throw;
            }

            return true;
        }

        private static void UninstallGalleryPackage(string pkgId)
        {
            // if installed from gallery, also remove NuGet package files
            NuGetFeed.Remove(pkgId, PackagesRoot);
        }

    }
}
