using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace Rehost.Web.ScenarioHost;

// What the captured-payload replay still needs in the host process: the action of the form the
// application rendered, and a body encoder for the fields the capture supplies. Scenarios that
// build their own postback do it in the test process, over PostbackForm.
public static class PostbackFormClient
{
    private static readonly Regex Attribute = new Regex(
        "(?<name>[\\w:-]+)\\s*=\\s*\"(?<value>[^\"]*)\"",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex FormTag = new Regex(
        "<form\\b[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static string FormAction(string html)
    {
        var form = FormTag.Match(html);
        if (!form.Success)
        {
            throw new InvalidOperationException("The response rendered no <form> element.");
        }

        foreach (Match attribute in Attribute.Matches(form.Value))
        {
            if (string.Equals(attribute.Groups["name"].Value, "action", StringComparison.OrdinalIgnoreCase))
            {
                return HttpUtility.HtmlDecode(attribute.Groups["value"].Value);
            }
        }

        throw new InvalidOperationException("The rendered <form> carries no action attribute.");
    }

    public static string Encode(IDictionary<string, string> fields)
    {
        var body = new StringBuilder();

        foreach (var entry in fields)
        {
            if (body.Length != 0)
            {
                body.Append('&');
            }

            body.Append(HttpUtility.UrlEncode(entry.Key));
            body.Append('=');
            body.Append(HttpUtility.UrlEncode(entry.Value));
        }

        return body.ToString();
    }
}
