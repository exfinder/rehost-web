namespace Rehost.WebForms.Hosting;

using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

        builder.Services.Configure<KestrelServerOptions>(kestrel =>
        {
            kestrel.ResponseHeaderEncodingSelector = ResponseHeaderEncoding.Select;
            kestrel.RequestHeaderEncodingSelector = _ => RequestHeaderEncoding.Instance;
        });

        // Behind a TLS-terminating proxy the scheme, host, and client address a Framework
        // application reads (IsSecureConnection, Url, UserHostAddress) arrive as X-Forwarded-*.
        // Registered here so a consumer never has to remember it; trust stays ASP.NET Core's —
        // loopback proxies unless ASPNETCORE_FORWARDEDHEADERS_ENABLED (which the framework's own
        // setup turns into "trust all" for For/Proto) or ForwardedHeadersOptions widens it.
        builder.Services.Configure<ForwardedHeadersOptions>(forwarded =>
            forwarded.ForwardedHeaders |= ForwardedHeaders.XForwardedFor
                | ForwardedHeaders.XForwardedProto
                | ForwardedHeaders.XForwardedHost);

        return builder;
    }

    // Terminal: System.Web owns every request that reaches this point, including producing its
    // own 404 when no handler is configured for the path.
    public static IApplicationBuilder UseRehostWebForms(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var activation = app.ApplicationServices.GetRequiredService<ClassicPipelineActivation>();
        var lifetime = app.ApplicationServices.GetRequiredService<IHostApplicationLifetime>();

        // AddRehostWebForms runs before the container exists, so the host's factory can only be
        // handed over here: after Build, before Kestrel accepts. Bootstrap-window events stay on
        // the event source.
        WebFormsApplication.AttachLoggerFactory(
            app.ApplicationServices.GetService<ILoggerFactory>());
        lifetime.ApplicationStopping.Register(activation.Shutdown);

        // The exit code is the AppDomain recycle's analog (ADR 0012).
        activation.RegisterRestartRequested(() =>
        {
            Environment.ExitCode = WebFormsExitCodes.RestartRequested;
            lifetime.StopApplication();
        });

        // With ASPNETCORE_FORWARDEDHEADERS_ENABLED the framework's startup filter has already put
        // the middleware at the head of the pipeline; a second pass would re-read what it left.
        var configuration = app.ApplicationServices.GetService<IConfiguration>();
        if (!string.Equals(configuration?["ForwardedHeaders_Enabled"], "true", StringComparison.OrdinalIgnoreCase))
        {
            app.UseForwardedHeaders();
        }

        // AcceptWebSocketRequest rides ASP.NET Core's WebSocket middleware (Kestrel alone exposes
        // only the raw upgrade); registered here so a consumer never has to remember it, options
        // overridable through WebSocketOptions in DI.
        app.UseWebSockets();

        var middleware = new RehostWebFormsMiddleware(activation);
        app.Run(middleware.InvokeAsync);

        return app;
    }
}
