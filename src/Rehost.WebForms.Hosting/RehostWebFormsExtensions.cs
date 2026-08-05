namespace Rehost.WebForms.Hosting;

using System;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public static class RehostWebFormsExtensions
{
    // Initialization happens here rather than on the first request so an unusable configuration
    // stops the process before the server ever listens.
    public static IHostApplicationBuilder AddRehostWebForms(
        this IHostApplicationBuilder builder,
        Action<WebFormsApplicationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new WebFormsApplicationOptions();
        configure(options);

        WebFormsApplication.Initialize(options);
        builder.Services.AddSingleton(new ClassicPipelineActivation(options));

        // Framework writes response headers in HttpResponse.HeaderEncoding, UTF-8 by default
        // (IIS7WorkerRequest encodes every header name and value with it); Kestrel's default
        // refuses non-ASCII header values outright. A non-default
        // <globalization responseHeaderEncoding> stays a documented residual gap.
        builder.Services.Configure<KestrelServerOptions>(
            kestrel => kestrel.ResponseHeaderEncodingSelector = _ => Encoding.UTF8);

        return builder;
    }

    // Terminal: System.Web owns every request that reaches this point, including producing its
    // own 404 when no handler is configured for the path.
    public static IApplicationBuilder UseRehostWebForms(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var activation = app.ApplicationServices.GetRequiredService<ClassicPipelineActivation>();
        var lifetime = app.ApplicationServices.GetRequiredService<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Register(activation.Shutdown);

        var middleware = new RehostWebFormsMiddleware(activation);
        app.Run(middleware.InvokeAsync);

        return app;
    }
}
