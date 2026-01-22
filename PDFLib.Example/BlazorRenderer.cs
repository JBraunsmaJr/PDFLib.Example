using Microsoft.AspNetCore.Components.Web;

namespace PDFLib.Example;

using Microsoft.AspNetCore.Components;

public class BlazorRenderer
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILoggerFactory _loggerFactory;

    public BlazorRenderer(IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
    {
        _serviceProvider = serviceProvider;
        _loggerFactory = loggerFactory;
    }

    public async Task<string> RenderComponent<T>(Dictionary<string, object?>? parameters = null) where T : IComponent
    {
        await using var renderer = new HtmlRenderer(_serviceProvider, _loggerFactory);

        var parameterView = parameters != null ? ParameterView.FromDictionary(parameters) : ParameterView.Empty;

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<T>(parameterView);
            return output.ToHtmlString();
        });

        return html;
    }
}