using System.Diagnostics;
using System.Globalization;
using System.Web;
using Rehost.WebForms.ScenarioProtocol;

namespace Rehost.WebForms.ScenarioProbes;

// Response.ClientDisconnectedToken and Request.Abort, which classic refused and integrated
// answered (IV24, IV25). Every outcome goes to the witness: the modes that matter have no client
// left to read a response.
public sealed class DisconnectProbe : IHttpHandler
{
    private static readonly TimeSpan CancelBudget = TimeSpan.FromSeconds(20);

    private static readonly TimeSpan ReadWindow = TimeSpan.FromMilliseconds(500);

    public bool IsReusable => false;

    public void ProcessRequest(HttpContext context)
    {
        switch (context.Request.QueryString["mode"])
        {
            case "token":
                Token(context);
                break;

            case "hold":
                Hold(context);
                break;

            case "abort":
                Abort(context);
                break;

            default:
                context.Response.Write("unknown-mode");
                break;
        }
    }

    private static void Token(HttpContext context)
    {
        Witness.Record(WitnessProtocol.DisconnectToken + Describe(context.Response.ClientDisconnectedToken));
        context.AddOnRequestCompleted(completed =>
            Witness.Record(
                WitnessProtocol.DisconnectCompleted + Describe(completed.Response.ClientDisconnectedToken)));
        context.Response.Write("token");
    }

    // The client leaves mid-response; Kestrel signals the token without the server writing again.
    private static void Hold(HttpContext context)
    {
        context.Response.Write("HOLDING");
        context.Response.Flush();

        var token = context.Response.ClientDisconnectedToken;
        var waited = Stopwatch.StartNew();
        while (!token.IsCancellationRequested && waited.Elapsed < CancelBudget)
        {
            Thread.Sleep(20);
        }

        Witness.Record(
            WitnessProtocol.DisconnectHeld + token.IsCancellationRequested.ToString(CultureInfo.InvariantCulture));
    }

    private static void Abort(HttpContext context)
    {
        var response = context.Response;
        response.Write("BEFORE-ABORT");
        response.Flush();

        // The reset discards whatever the client has not read yet, so the flushed bytes need a
        // moment on the wire before it goes out.
        Thread.Sleep(ReadWindow);

        context.Request.Abort();
        Witness.Record(WitnessProtocol.AbortReturned + "returned");
        Witness.Record(
            WitnessProtocol.AbortConnected
            + response.IsClientConnected.ToString(CultureInfo.InvariantCulture));

        response.Write("AFTER-ABORT");
        try
        {
            response.Flush();
            Witness.Record(WitnessProtocol.AbortFlushed + "ok");
        }
        catch (Exception failure)
        {
            Witness.Record(
                WitnessProtocol.AbortFlushed + failure.GetType().Name + ":" + failure.Message);
        }
    }

    private static string Describe(System.Threading.CancellationToken token) =>
        token.CanBeCanceled.ToString(CultureInfo.InvariantCulture)
        + "/"
        + token.IsCancellationRequested.ToString(CultureInfo.InvariantCulture);
}
