using System;
using System.Collections.Generic;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.RomanizedTitles.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the IMDb ids (tt…) the plugin must leave alone, separated by spaces, commas or new lines.
    /// </summary>
    public string ExcludedImdbIds { get; set; } = string.Empty;

    /// <summary>
    /// Gets the excluded IMDb ids as a set.
    /// </summary>
    /// <returns>The excluded ids.</returns>
    public IReadOnlySet<string> GetExcludedImdbIds()
    {
        return new HashSet<string>(
            ExcludedImdbIds.Split([' ', ',', ';', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries),
            StringComparer.OrdinalIgnoreCase);
    }
}
