using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.RomanizedTitles;

/// <summary>
/// Register plugin services.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc/>
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<ImdbTitleStore>();
        serviceCollection.AddSingleton<TitleUpdater>();
        serviceCollection.AddHostedService(provider => provider.GetRequiredService<TitleUpdater>());
    }
}
