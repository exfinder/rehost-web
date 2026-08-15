namespace Rehost.WebForms.Owin.Host.SystemWeb.Tests.Loader;

internal sealed class StubAppBuilder : global::Owin.IAppBuilder
{
    public IDictionary<string, object> Properties { get; } = new Dictionary<string, object>();

    public global::Owin.IAppBuilder Use(object middleware, params object[] args) => this;

    public global::Owin.IAppBuilder New() => new StubAppBuilder();

    public object Build(Type returnType) => throw new NotSupportedException();
}
