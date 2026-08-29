using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace WingtipToys.Host;

internal static class PayPalNvpResponder
{
    private const string SandboxEndpointPrefix = "https://api-3t.sandbox.paypal.com";
    private const string PayerId = "WINGTIPPAYER01";

    private static readonly ConcurrentDictionary<string, string> AmountByToken = new(StringComparer.Ordinal);

    public static void Start()
    {
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0));

        var responder = builder.Build();
        responder.Run(RespondAsync);
        responder.Start();

        WebRequest.RegisterPrefix(SandboxEndpointPrefix, new Redirector(responder.Urls.Single()));
    }

    private static async Task RespondAsync(HttpContext context)
    {
        string body;
        using (var reader = new StreamReader(context.Request.Body, Encoding.ASCII))
        {
            body = await reader.ReadToEndAsync();
        }

        var request = Decode(body);
        var response = request.GetValueOrDefault("METHOD") switch
        {
            "SetExpressCheckout" => SetExpressCheckout(request),
            "GetExpressCheckoutDetails" => GetExpressCheckoutDetails(request),
            "DoExpressCheckoutPayment" => DoExpressCheckoutPayment(request),
            var method => Failure("10001", "Method unsupported", $"'{method}' is not implemented by the fixture responder."),
        };

        context.Response.ContentType = "text/plain; charset=utf-8";
        await context.Response.WriteAsync(Encode(response), Encoding.ASCII);
    }

    private static Dictionary<string, string> SetExpressCheckout(Dictionary<string, string> request)
    {
        var amount = request.GetValueOrDefault("PAYMENTREQUEST_0_AMT");
        if (amount is null)
        {
            return Failure("10400", "Transaction refused", "Order total is missing.");
        }

        var token = $"EC-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        AmountByToken[token] = amount;

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["TOKEN"] = token,
            ["TIMESTAMP"] = Timestamp(),
            ["ACK"] = "Success",
            ["VERSION"] = "88.0",
        };
    }

    private static Dictionary<string, string> GetExpressCheckoutDetails(Dictionary<string, string> request)
    {
        if (!AmountByToken.TryGetValue(request.GetValueOrDefault("TOKEN") ?? string.Empty, out var amount))
        {
            return Failure("10410", "Invalid token", "The token is not recognized by the fixture responder.");
        }

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["TOKEN"] = request["TOKEN"],
            ["TIMESTAMP"] = Timestamp(),
            ["ACK"] = "Success",
            ["PAYERID"] = PayerId,
            ["FIRSTNAME"] = "Wingtip",
            ["LASTNAME"] = "Shopper",
            ["EMAIL"] = "wingtip.shopper@example.com",
            ["SHIPTOSTREET"] = "1 Microsoft Way",
            ["SHIPTOCITY"] = "Redmond",
            ["SHIPTOSTATE"] = "WA",
            ["SHIPTOZIP"] = "98052",
            ["SHIPTOCOUNTRYCODE"] = "US",
            ["AMT"] = amount,
        };
    }

    private static Dictionary<string, string> DoExpressCheckoutPayment(Dictionary<string, string> request)
    {
        var token = request.GetValueOrDefault("TOKEN") ?? string.Empty;
        if (!AmountByToken.TryGetValue(token, out var amount))
        {
            return Failure("10410", "Invalid token", "The token is not recognized by the fixture responder.");
        }

        if (request.GetValueOrDefault("PAYERID") != PayerId)
        {
            return Failure("10408", "Missing payer id", "The payer id does not match the one issued for this token.");
        }

        AmountByToken.TryRemove(token, out _);

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["TOKEN"] = token,
            ["TIMESTAMP"] = Timestamp(),
            ["ACK"] = "Success",
            ["PAYMENTINFO_0_TRANSACTIONID"] = Guid.NewGuid().ToString("N")[..17].ToUpperInvariant(),
            ["PAYMENTINFO_0_AMT"] = amount,
            ["PAYMENTINFO_0_PAYMENTSTATUS"] = "Completed",
        };
    }

    private static Dictionary<string, string> Failure(string code, string shortMessage, string longMessage) =>
        new(StringComparer.Ordinal)
        {
            ["TIMESTAMP"] = Timestamp(),
            ["ACK"] = "Failure",
            ["VERSION"] = "88.0",
            ["L_ERRORCODE0"] = code,
            ["L_SHORTMESSAGE0"] = shortMessage,
            ["L_LONGMESSAGE0"] = longMessage,
        };

    private static string Timestamp() =>
        DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    private static Dictionary<string, string> Decode(string nvp)
    {
        var decoded = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in nvp.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            if (separator > 0)
            {
                decoded[Unescape(pair[..separator])] = Unescape(pair[(separator + 1)..]);
            }
        }

        return decoded;
    }

    private static string Unescape(string value) => Uri.UnescapeDataString(value.Replace('+', ' '));

    private static string Encode(Dictionary<string, string> nvp) =>
        string.Join('&', nvp.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));

    private sealed class Redirector(string address) : IWebRequestCreate
    {
        public WebRequest Create(Uri uri) => WebRequest.Create($"{address.TrimEnd('/')}{uri.PathAndQuery}");
    }
}
