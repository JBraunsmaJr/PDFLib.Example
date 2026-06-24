using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components;
using HtmlAgilityPack;
using PDFLib.Example.Components.Pages;

namespace PDFLib.Example;

public class BlazorRenderer
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<BlazorRenderer> _logger;

    /// <summary>
    /// Absolute URLs whose host matches one of these are still treated as "local"
    /// (e.g. if a component ever emits a fully qualified self-referencing URL).
    /// Add production hostname(s) here if that ever applied
    /// </summary>
    private readonly HashSet<string> _localHosts;

    public BlazorRenderer(
        IServiceProvider serviceProvider, 
        ILoggerFactory loggerFactory,
        IWebHostEnvironment env,
        IEnumerable<string>? additionalLocalhosts = null)
    {
        _serviceProvider = serviceProvider;
        _loggerFactory = loggerFactory;
        _env = env;
        _logger = loggerFactory.CreateLogger<BlazorRenderer>();

        _localHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "localhost",
            "127.0.0.1",
            "::1"
        };

        if (additionalLocalhosts is not null)
            _localHosts.UnionWith(additionalLocalhosts);
    }
    
    
    /// <summary>
    /// Render a component into HTML. This does NOT include the layout EVEN if the component has a @layout directive.
    /// </summary>
    /// <param name="parameters"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public async Task<string> RenderComponent<T>(Dictionary<string, object?>? parameters = null) where T : IComponent
    {
        await using var renderer = new HtmlRenderer(_serviceProvider, _loggerFactory);
        
        var parameterView = parameters != null ? ParameterView.FromDictionary(parameters) : ParameterView.Empty;

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<T>(parameterView);
            return output.ToHtmlString();
        });
        
        // There is no point in inlining here because there won't be any CSS/JS resources
        _logger.LogInformation("Page HTML: {HTML}", html);
        return html;
    }

    /// <summary>
    /// There is a special Wrapper component <see cref="Wrapper{TContent}"/> that can be used to wrap any component
    /// so that it contains the appropriate CSS/JS resources
    /// </summary>
    /// <param name="parameters"></param>
    /// <typeparam name="TContent"></typeparam>
    /// <returns></returns>
    public async Task<string> RenderWrappedComponent<TContent>(Dictionary<string, object?>? parameters = null)
    {
        await using var renderer = new HtmlRenderer(_serviceProvider, _loggerFactory);
        var parameterView = parameters != null ? ParameterView.FromDictionary(parameters) : ParameterView.Empty;

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<Wrapper<TContent>>(parameterView);
            return output.ToHtmlString();
        });

        var pageHtml = await InlineLocalResourcesAsync(html);
        _logger.LogInformation("Page HTML: {HTML}", pageHtml);
        return pageHtml;
    }

    /// <summary>
    /// Rewrites any locally hosted &lt;link rel="stylesheet"&gt; / &lt;script src&gt;
    /// references into inline tags so the returned markup has no outstanding
    /// network dependencies. External resources are left untouched
    /// </summary>
    /// <param name="html"></param>
    /// <returns></returns>
    private async Task<string> InlineLocalResourcesAsync(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        
        // Cache so a resource referenced more than once is only read from disk once
        var cache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        await InlineStylesheetsAsync(doc, cache);
        await InlineScriptsAsync(doc, cache);

        return doc.DocumentNode.OuterHtml;
    }

    private async Task InlineStylesheetsAsync(HtmlDocument doc, Dictionary<string, string?> cache)
    {
        var linkNodes = doc.DocumentNode.SelectNodes("//link[@href]")?.ToList();
        if(linkNodes is null) return;

        foreach (var link in linkNodes)
        {
            var relTokens = link.GetAttributeValue("rel", "")
                .Split(" ", StringSplitOptions.RemoveEmptyEntries);

            if (!relTokens.Contains("stylesheet", StringComparer.OrdinalIgnoreCase))
                continue; // not a stylesheet

            var href = link.GetAttributeValue("href", string.Empty);
            if(string.IsNullOrWhiteSpace(href) || !IsLocalResource(href))
                continue;

            var content = await GetLocalResourceContentAsync(href, cache);
            if (content is null)
            {
                _logger.LogWarning("Could not resolve local stylesheet '{Href}' for inlining; leaving <link> as-is", href);
                continue;
            }

            var mediaAttr = link.Attributes["media"] is not null
                ? $" media=\"{link.GetAttributeValue("media", "")}\""
                : string.Empty;

            var styleNode = HtmlNode.CreateNode($"<style{mediaAttr}>{content}</style>");
            link.ParentNode.ReplaceChild(styleNode, link);
        }
    }

    private async Task InlineScriptsAsync(HtmlDocument doc, Dictionary<string, string?> cache)
    {
        var scriptNodes = doc.DocumentNode.SelectNodes("//script[@src]")?.ToList();
        if (scriptNodes is null) return;

        foreach (var script in scriptNodes)
        {
            var src = script.GetAttributeValue("src", string.Empty);
            if (string.IsNullOrWhiteSpace(src) || !IsLocalResource(src))
                continue; // external

            var content = await GetLocalResourceContentAsync(src, cache);
            if (content is null)
            {
                _logger.LogWarning("Could not resolve local script '{Src}' for inlining; leaving <script> as-is", src);
                continue;
            }

            var typeAttr = script.Attributes["types"] is not null
                ? $" type=\"{script.GetAttributeValue("type", "")}\""
                : string.Empty;

            var safeContent = content.Replace("</script", "<\\/script", StringComparison.OrdinalIgnoreCase);
            var scriptNode = HtmlNode.CreateNode($"<script{typeAttr}>{safeContent}</script>");
            script.ParentNode.ReplaceChild(scriptNode, script);
        }
    }
    
    ///<summary>
    /// ///A URL is "local" if it's relative/root-relative (no scheme), or an absolute
    /// URL whose host explicitly points back at this server. Anything else
    /// (a CDN, Google Fonts, etc.) are treated as external and left alone
    /// </summary
    private bool IsLocalResource(string url)
    {
        if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return false; // already inline

        if (url.StartsWith("//"))
            return false; // protocol-relative -> points at some other host

        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
            return _localHosts.Contains(absolute.Host);

        return true; // relative or root-relative path
    }

    private async Task<string?> GetLocalResourceContentAsync(string url, Dictionary<string, string?> cache)
    {
        var path = url.Split('?', '#')[0].TrimStart("/").ToString();
        
        if (cache.TryGetValue(path, out var cached))
            return cached;

        var fileInfo = _env.WebRootFileProvider.GetFileInfo(path);
        string? content = null;

        if (fileInfo.Exists)
        {
            await using var stream = fileInfo.CreateReadStream();
            using var reader = new StreamReader(stream);
            content = await reader.ReadToEndAsync();
        }

        cache[path] = content;
        return content;
    }
}