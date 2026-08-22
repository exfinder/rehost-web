using System.Runtime.CompilerServices;
using Rehost.WebForms.Parity.Contracts;

namespace Rehost.WebForms.Hosting.Tests;

// Class + method makes tokens collision-free between classes interleaving on a shared host; a
// bare literal reused by two classes would silently pool their stages. Method name alone is not
// enough: classes legitimately repeat method names.
internal static class WitnessToken
{
    internal static string For(object testClass, [CallerMemberName] string method = "")
    {
        if (testClass is LiveScenario or Scenario)
        {
            throw new ArgumentException(
                "Pass the test class, not the scenario: the token must carry the class whose"
                + " stages it keys.",
                nameof(testClass));
        }

        return testClass.GetType().Name + "." + method;
    }

    internal static string Query(string token) => WitnessProtocol.TokenKey + "=" + token;
}
