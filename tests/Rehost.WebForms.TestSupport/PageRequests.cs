namespace Rehost.WebForms.TestSupport;

// The canonical Default.aspx request: query characters chosen to prove encoding survives
// compilation and dispatch. The redirect target pairs with the page fixture's Redirect.aspx.
public static class PageRequests
{
    public const string Canonical = "/Default.aspx?value=a%26c%20%22q%22%20%C3%A9";
    public const string RedirectTarget = "/Default.aspx?value=r";
}
