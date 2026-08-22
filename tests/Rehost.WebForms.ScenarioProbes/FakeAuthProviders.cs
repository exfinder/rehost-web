using System.Collections.Concurrent;
using System.Configuration;
using System.Web.Profile;
using System.Web.Security;

namespace Rehost.WebForms.ScenarioProbes;

public sealed class FakeMembershipProvider : MembershipProvider
{
    public const string KnownUser = "alice";
    public const string KnownPassword = "pw";

    private string applicationName = "/";

    public override bool ValidateUser(string username, string password)
        => !string.IsNullOrEmpty(username) && password == KnownPassword;

    public override string ApplicationName
    {
        get => applicationName;
        set => applicationName = value;
    }

    public override MembershipUser? GetUser(string username, bool userIsOnline)
        => username == KnownUser
            ? new MembershipUser(
                Name,
                KnownUser,
                KnownUser,
                "alice@example.test",
                string.Empty,
                string.Empty,
                true,
                false,
                DateTime.UtcNow,
                DateTime.UtcNow,
                DateTime.UtcNow,
                DateTime.UtcNow,
                DateTime.UtcNow)
            : null;

    public override bool EnablePasswordRetrieval => false;

    public override bool EnablePasswordReset => false;

    public override bool RequiresQuestionAndAnswer => false;

    public override bool RequiresUniqueEmail => false;

    public override int MaxInvalidPasswordAttempts => 5;

    public override int PasswordAttemptWindow => 10;

    public override MembershipPasswordFormat PasswordFormat => MembershipPasswordFormat.Hashed;

    public override int MinRequiredPasswordLength => 1;

    public override int MinRequiredNonAlphanumericCharacters => 0;

    public override string PasswordStrengthRegularExpression => string.Empty;

    public override MembershipUser? CreateUser(
        string username,
        string password,
        string email,
        string passwordQuestion,
        string passwordAnswer,
        bool isApproved,
        object providerUserKey,
        out MembershipCreateStatus status)
    {
        status = MembershipCreateStatus.ProviderError;
        return null;
    }

    public override bool ChangePasswordQuestionAndAnswer(
        string username,
        string password,
        string newPasswordQuestion,
        string newPasswordAnswer) => false;

    public override string GetPassword(string username, string answer)
        => throw new NotSupportedException();

    public override bool ChangePassword(string username, string oldPassword, string newPassword)
        => false;

    public override string ResetPassword(string username, string answer)
        => throw new NotSupportedException();

    public override void UpdateUser(MembershipUser user)
    {
    }

    public override bool UnlockUser(string userName) => false;

    public override MembershipUser? GetUser(object providerUserKey, bool userIsOnline) => null;

    public override string? GetUserNameByEmail(string email) => null;

    public override bool DeleteUser(string username, bool deleteAllRelatedData) => false;

    public override MembershipUserCollection GetAllUsers(int pageIndex, int pageSize, out int totalRecords)
    {
        totalRecords = 0;
        return new MembershipUserCollection();
    }

    public override int GetNumberOfUsersOnline() => 0;

    public override MembershipUserCollection FindUsersByName(
        string usernameToMatch,
        int pageIndex,
        int pageSize,
        out int totalRecords)
    {
        totalRecords = 0;
        return new MembershipUserCollection();
    }

    public override MembershipUserCollection FindUsersByEmail(
        string emailToMatch,
        int pageIndex,
        int pageSize,
        out int totalRecords)
    {
        totalRecords = 0;
        return new MembershipUserCollection();
    }
}

public sealed class FakeRoleProvider : RoleProvider
{
    public const string KnownRole = "editors";

    private string applicationName = "/";

    private static readonly ConcurrentDictionary<string, int> Fetches = new();

    public static int FetchesFor(string username) => Fetches.TryGetValue(username, out var count) ? count : 0;

    public override string[] GetRolesForUser(string username)
    {
        Fetches.AddOrUpdate(username, 1, (_, count) => count + 1);
        return [KnownRole];
    }

    public override bool IsUserInRole(string username, string roleName) => roleName == KnownRole;

    public override string ApplicationName
    {
        get => applicationName;
        set => applicationName = value;
    }

    public override void CreateRole(string roleName)
    {
    }

    public override bool DeleteRole(string roleName, bool throwOnPopulatedRole) => false;

    public override bool RoleExists(string roleName) => roleName == KnownRole;

    public override void AddUsersToRoles(string[] usernames, string[] roleNames)
    {
    }

    public override void RemoveUsersFromRoles(string[] usernames, string[] roleNames)
    {
    }

    public override string[] GetUsersInRole(string roleName) => [];

    public override string[] GetAllRoles() => [KnownRole];

    public override string[] FindUsersInRole(string roleName, string usernameToMatch) => [];
}

public sealed class FakeProfileProvider : ProfileProvider
{
    private static readonly ConcurrentDictionary<string, string> Values = new();

    private string applicationName = "/";

    public override string ApplicationName
    {
        get => applicationName;
        set => applicationName = value;
    }

    public override SettingsPropertyValueCollection GetPropertyValues(
        SettingsContext context,
        SettingsPropertyCollection collection)
    {
        var owner = (string?)context["UserName"] ?? string.Empty;
        var values = new SettingsPropertyValueCollection();

        foreach (SettingsProperty property in collection)
        {
            var value = new SettingsPropertyValue(property);

            if (Values.TryGetValue(Key(owner, property.Name), out var stored))
            {
                value.PropertyValue = stored;
            }

            values.Add(value);
        }

        return values;
    }

    public override void SetPropertyValues(
        SettingsContext context,
        SettingsPropertyValueCollection collection)
    {
        var owner = (string?)context["UserName"] ?? string.Empty;

        foreach (SettingsPropertyValue value in collection)
        {
            if (value.PropertyValue is null)
            {
                continue;
            }

            Values[Key(owner, value.Property.Name)] = Convert.ToString(value.PropertyValue) ?? string.Empty;
        }
    }

    public override int DeleteProfiles(ProfileInfoCollection profiles) => 0;

    public override int DeleteProfiles(string[] usernames) => 0;

    public override int DeleteInactiveProfiles(
        ProfileAuthenticationOption authenticationOption,
        DateTime userInactiveSinceDate) => 0;

    public override int GetNumberOfInactiveProfiles(
        ProfileAuthenticationOption authenticationOption,
        DateTime userInactiveSinceDate) => 0;

    public override ProfileInfoCollection GetAllProfiles(
        ProfileAuthenticationOption authenticationOption,
        int pageIndex,
        int pageSize,
        out int totalRecords)
    {
        totalRecords = 0;
        return new ProfileInfoCollection();
    }

    public override ProfileInfoCollection GetAllInactiveProfiles(
        ProfileAuthenticationOption authenticationOption,
        DateTime userInactiveSinceDate,
        int pageIndex,
        int pageSize,
        out int totalRecords)
    {
        totalRecords = 0;
        return new ProfileInfoCollection();
    }

    public override ProfileInfoCollection FindProfilesByUserName(
        ProfileAuthenticationOption authenticationOption,
        string usernameToMatch,
        int pageIndex,
        int pageSize,
        out int totalRecords)
    {
        totalRecords = 0;
        return new ProfileInfoCollection();
    }

    public override ProfileInfoCollection FindInactiveProfilesByUserName(
        ProfileAuthenticationOption authenticationOption,
        string usernameToMatch,
        DateTime userInactiveSinceDate,
        int pageIndex,
        int pageSize,
        out int totalRecords)
    {
        totalRecords = 0;
        return new ProfileInfoCollection();
    }

    private static string Key(string owner, string property) => owner + "/" + property;
}
