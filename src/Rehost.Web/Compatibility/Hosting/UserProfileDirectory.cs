namespace Rehost.Web.Hosting;

using System;

internal static class UserProfileDirectory
{
    internal const string FolderName = ".rehost";

    internal static string Resolve()
    {
        var profile = Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile,
            Environment.SpecialFolderOption.DoNotVerify);
        return String.IsNullOrEmpty(profile) ? null : profile;
    }
}
