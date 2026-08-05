using System.Text;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// The cap is off on both runtimes unless an application asks for it — .NET Framework 4.8.1 reads
// Int32.MaxValue, and this port ships the same constant and no key. This application opts in at
// 1000, the value Microsoft's 2011 hash-collision patch shipped, so the check is reachable at all.
// It runs before each add, so exactly 1000 is accepted and the next one is refused.
public sealed class CollectionKeyLimitOverKestrelTests(CollectionKeysLiveScenario scenario)
    : IClassFixture<CollectionKeysLiveScenario>
{
    private const int Cap = 1000;

    private const string Refusal =
        "error:System.InvalidOperationException:The maximum number of form, query string, or "
        + "posted file items has already been read from the request. To change the maximum allowed "
        + "request collection count from its current value of 1000, change the "
        + "\"aspnet:MaxHttpCollectionKeys\" setting.";

    [Fact]
    public async Task A_Query_String_At_The_Limit_Is_Read_Whole()
    {
        var response = await scenario.Client.GetAsync("/keys?" + Keys(Cap));

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe("count:" + Cap);
    }

    [Fact]
    public async Task A_Query_String_Past_The_Limit_Is_Refused()
    {
        var response = await scenario.Client.GetAsync("/keys?" + Keys(Cap + 1));

        response.Text.ShouldStartWith(Refusal);
    }

    // The urlencoded parser catches everything and rethrows one "not valid" HttpException, so here
    // the limit is only distinguishable from a malformed body by the inner exception.
    [Fact]
    public async Task An_Urlencoded_Form_Past_The_Limit_Is_Refused()
    {
        var response = await scenario.Client.PostFormAsync(
            "/keys?collection=form",
            Keys(Cap + 1));

        response.Text.ShouldStartWith(
            "error:System.Web.HttpException:The URL-encoded form data is not valid.|inner:"
            + Refusal["error:".Length..]);
    }

    // A multipart body reaches the check from HttpRequest's own loop over the parsed elements
    // rather than from the urlencoded parser above.
    [Fact]
    public async Task A_Multipart_Form_Past_The_Limit_Is_Refused()
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i <= Cap; i++)
        {
            fields["k" + i] = "v";
        }

        var response = await PostMultipartAsync("form", fields, []);

        response.Text.ShouldStartWith(Refusal);
    }

    // HttpFileCollection carries its own copy of the check, counting parts rather than fields.
    [Fact]
    public async Task Posted_Files_Past_The_Limit_Are_Refused()
    {
        var files = new List<MultipartFile>();
        for (var i = 0; i <= Cap; i++)
        {
            files.Add(new MultipartFile("f" + i, "f" + i + ".txt", "text/plain", [0x79]));
        }

        var response = await PostMultipartAsync(
            "files",
            new Dictionary<string, string>(StringComparer.Ordinal),
            files);

        response.Text.ShouldStartWith(Refusal);
    }

    private Task<ScenarioResponse> PostMultipartAsync(
        string collection,
        IDictionary<string, string> fields,
        IList<MultipartFile> files) =>
        scenario.Client.PostAsync(
            "/keys?collection=" + collection,
            PostbackForm.EncodeMultipart(fields, files),
            "multipart/form-data; boundary=" + PostbackForm.MultipartBoundary);

    // Keys stay short because a query string also has to fit Kestrel's 8 KB request line, which is
    // a host limit unrelated to this one.
    private static string Keys(int count)
    {
        var keys = new StringBuilder();
        for (var i = 0; i < count; i++)
        {
            if (keys.Length != 0)
            {
                keys.Append('&');
            }

            // The '=' matters: a token without one is added under a null name, so every
            // such token lands on the same entry and the count never grows.
            keys.Append('k').Append(i).Append('=');
        }

        return keys.ToString();
    }
}
