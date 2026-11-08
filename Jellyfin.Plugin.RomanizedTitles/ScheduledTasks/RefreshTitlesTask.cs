using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RomanizedTitles.ScheduledTasks;

/// <summary>
/// Downloads IMDb's title list again and checks the whole library.
/// </summary>
public class RefreshTitlesTask : IScheduledTask
{
    private readonly ImdbTitleStore _titleStore;
    private readonly TitleUpdater _titleUpdater;
    private readonly ILogger<RefreshTitlesTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshTitlesTask"/> class.
    /// </summary>
    /// <param name="titleStore">The IMDb title list.</param>
    /// <param name="titleUpdater">The title updater.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{RefreshTitlesTask}"/> interface.</param>
    public RefreshTitlesTask(ImdbTitleStore titleStore, TitleUpdater titleUpdater, ILogger<RefreshTitlesTask> logger)
    {
        _titleStore = titleStore;
        _titleUpdater = titleUpdater;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Name => "Apply romanized titles";

    /// <inheritdoc/>
    public string Key => "RomanizedTitlesRefresh";

    /// <inheritdoc/>
    public string Description => "Downloads IMDb's title list and renames Indian films and series to their original title in Roman script.";

    /// <inheritdoc/>
    public string Category => "Romanized Titles";

    /// <inheritdoc/>
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        await _titleStore.DownloadAsync(cancellationToken).ConfigureAwait(false);
        progress.Report(80);
        var renamed = await _titleUpdater.ApplyToLibraryAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Romanized titles applied: {Count} renamed", renamed);
        progress.Report(100);
    }

    /// <inheritdoc/>
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return [new TaskTriggerInfo { Type = TaskTriggerInfoType.IntervalTrigger, IntervalTicks = TimeSpan.FromDays(7).Ticks }];
    }
}
