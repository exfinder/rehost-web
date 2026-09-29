namespace Microsoft.AspNet.FriendlyUrls.Resolvers;

public class GenericHandlerFriendlyUrlResolver : FriendlyUrlResolver
{
    public GenericHandlerFriendlyUrlResolver()
        : base(".ashx")
    {
    }
}
