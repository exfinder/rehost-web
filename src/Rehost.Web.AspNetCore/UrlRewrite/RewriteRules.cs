namespace Rehost.Web.AspNetCore;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web.Hosting;
using System.Web.IisConfig;
using ForbiddenExtensions = System.Web.ForbiddenExtensions;
using HiddenSegments = System.Web.HiddenSegments;
using RequestFiltering = System.Web.RequestFiltering;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

internal enum RewriteOutcomeKind
{
    Unchanged,
    Rewritten,
    Answered,
}

internal readonly struct RewriteOutcome(RewriteOutcomeKind kind, string? originalUrl)
{
    internal RewriteOutcomeKind Kind { get; } = kind;

    internal string? OriginalUrl { get; } = originalUrl;
}

// The parser's own answers for the abort, the query order, the Location shape and the response
// body differ from IIS, so this owns all four rather than the middleware.
internal sealed class RewriteRules
{
    private readonly IReadOnlyList<IRule> _rules;
    private readonly IFileProvider _files;

    private RewriteRules(IReadOnlyList<IRule> rules, IFileProvider files)
    {
        _rules = rules;
        _files = files;
    }

    internal static RewriteRules? Load(RewriteSection? section, string physicalRootPath) =>
        section == null ? null : Load(section.Xml, section.ConfigPath, physicalRootPath);

    internal static RewriteRules Load(string xml, string configPath, string physicalRootPath)
    {
        RewriteOptions options;
        try
        {
            options = new RewriteOptions().AddIISUrlRewrite(new StringReader(xml));
        }
        catch (Exception failure)
            when (failure is FormatException or NotSupportedException or ArgumentException)
        {
            throw new InvalidOperationException(
                $"The <rewrite> section in '{configPath}' cannot be honored: {failure.Message}",
                failure);
        }

        return new RewriteRules(
            [.. options.Rules], new DirectoryAwareFileProvider(physicalRootPath));
    }

    internal async Task<RewriteOutcome> ApplyAsync(HttpContext context)
    {
        var request = context.Request;
        request.Path = new PathString(
            RequestPathCanonicalizer.Canonicalize(request.Path.Value ?? "/", out _));

        var originalPath = request.PathBase.Add(request.Path);
        var originalQuery = request.QueryString;

        var (filePath, _) = RequestPathInfo.Split(request.Method, originalPath.Value!);
        if (HiddenSegments.Refuses(filePath)
            || ForbiddenExtensions.Refuses(filePath)
            || RequestFiltering.Judge(request.Method, filePath, RawQuery(originalQuery)) != null)
        {
            return new RewriteOutcome(RewriteOutcomeKind.Unchanged, null);
        }

        if (Run(context) == RuleResult.EndResponse)
        {
            await AnswerAsync(context);
            return new RewriteOutcome(RewriteOutcomeKind.Answered, null);
        }

        if (request.PathBase.Add(request.Path) == originalPath
            && request.QueryString == originalQuery)
        {
            return new RewriteOutcome(RewriteOutcomeKind.Unchanged, null);
        }

        request.QueryString = RuleQueryFirst(originalQuery, request.QueryString);
        request.Headers["X-Original-URL"] =
            originalPath.ToUriComponent() + originalQuery.ToUriComponent();

        return new RewriteOutcome(
            RewriteOutcomeKind.Rewritten,
            originalPath.Value + originalQuery.ToUriComponent());
    }

    private RuleResult Run(HttpContext context)
    {
        var rewriteContext = new RewriteContext
        {
            HttpContext = context,
            StaticFileProvider = _files,
            Logger = NullLogger.Instance,
            Result = RuleResult.ContinueRules,
        };

        // A CustomResponse rule writes its statusDescription into the response body as it runs,
        // which would commit the response before the port writes the body IIS sent instead.
        var body = context.Response.Body;
        context.Response.Body = Stream.Null;
        try
        {
            foreach (var rule in _rules)
            {
                rule.ApplyRule(rewriteContext);
                if (rewriteContext.Result != RuleResult.ContinueRules)
                {
                    break;
                }
            }
        }
        finally
        {
            context.Response.Body = body;
        }

        return rewriteContext.Result;
    }

    private static async Task AnswerAsync(HttpContext context)
    {
        var response = context.Response;
        var location = response.Headers.Location.ToString();
        if (location.Length != 0)
        {
            var absolute = RequestUrls.Resolve(context.Request, location);
            response.Headers.Location = absolute;
            SetReasonPhrase(context, response.StatusCode);
            await WriteAsync(response, "text/html; charset=UTF-8", IisErrorBodies.ObjectMoved(absolute));
            return;
        }

        // AbortRequest is the rule that ends the request having set nothing: the importer leaves
        // the status at 200 and writes no body, where IIS reset the connection.
        if (response.StatusCode == StatusCodes.Status200OK)
        {
            context.Abort();
            return;
        }

        await WriteAsync(response, "text/html", IisErrorBodies.Refusal(response.StatusCode));
    }

    private static string RawQuery(QueryString query) =>
        query.HasValue ? query.Value![1..] : string.Empty;

    private static void SetReasonPhrase(HttpContext context, int status)
    {
        var phrase = status switch
        {
            StatusCodes.Status301MovedPermanently => "Moved Permanently",
            StatusCodes.Status302Found => "Redirect",
            StatusCodes.Status307TemporaryRedirect => "Moved Temporarily",
            _ => null,
        };

        var feature = context.Features.Get<IHttpResponseFeature>();
        if (phrase != null && feature != null)
        {
            feature.ReasonPhrase = phrase;
        }
    }

    private static Task WriteAsync(HttpResponse response, string contentType, string body)
    {
        response.ContentType = contentType;
        response.ContentLength = Encoding.UTF8.GetByteCount(body);
        return response.WriteAsync(body);
    }

    // IIS put the rule's own query first and the request's after it; the importer appends the
    // other way round.
    private static QueryString RuleQueryFirst(QueryString original, QueryString rewritten)
    {
        var carried = original.ToUriComponent();
        if (carried.Length <= 1)
        {
            return rewritten;
        }

        var appended = rewritten.ToUriComponent();
        var request = carried[1..];
        return appended.StartsWith($"?{request}&", StringComparison.Ordinal)
            ? new QueryString($"?{appended[(request.Length + 2)..]}&{request}")
            : rewritten;
    }
}
