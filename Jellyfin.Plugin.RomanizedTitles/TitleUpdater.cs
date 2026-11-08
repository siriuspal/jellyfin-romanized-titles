using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RomanizedTitles;

/// <summary>
/// Renames Indian films and series to IMDb's original title and locks the name,
/// both when Jellyfin updates an item and when the whole library is checked.
/// </summary>
public sealed class TitleUpdater : IHostedService, IDisposable
{
    // Waits this long after the last item update, so a library scan is handled in one pass.
    private const int QuietPeriodMs = 30_000;

    private readonly ILibraryManager _libraryManager;
    private readonly ImdbTitleStore _titleStore;
    private readonly ILogger<TitleUpdater> _logger;
    private readonly Timer _timer;
    private readonly HashSet<Guid> _queuedItemIds = [];
    private readonly Lock _queueLock = new();
    private readonly SemaphoreSlim _passLock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TitleUpdater"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="titleStore">The IMDb title list.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{TitleUpdater}"/> interface.</param>
    public TitleUpdater(ILibraryManager libraryManager, ImdbTitleStore titleStore, ILogger<TitleUpdater> logger)
    {
        _libraryManager = libraryManager;
        _titleStore = titleStore;
        _logger = logger;
        _timer = new Timer(_ => OnTimerElapsed(), null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    /// Checks every film and series in the library.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of items renamed.</returns>
    public Task<int> ApplyToLibraryAsync(CancellationToken cancellationToken)
    {
        var items = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Series],
            IsVirtualItem = false,
            Recursive = true,
            HasImdbId = true
        });

        return ApplyAsync(items, cancellationToken);
    }

    private async Task<int> ApplyAsync(IReadOnlyList<BaseItem> items, CancellationToken cancellationToken)
    {
        var excluded = Plugin.Instance!.Configuration.GetExcludedImdbIds();
        var candidates = items
            .Where(item => IsCandidate(item) && !excluded.Contains(item.GetProviderId(MetadataProvider.Imdb)!))
            .ToList();
        if (candidates.Count == 0)
        {
            return 0;
        }

        await _passLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var ids = candidates.Select(item => item.GetProviderId(MetadataProvider.Imdb)!).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var imdbTitles = await _titleStore.LookupAsync(ids, cancellationToken).ConfigureAwait(false);

            var renamed = 0;
            foreach (var item in candidates)
            {
                imdbTitles.TryGetValue(item.GetProviderId(MetadataProvider.Imdb)!, out var imdbTitle);
                var title = TitleRules.PickTitle(item.Name, item.OriginalTitle, imdbTitle);
                if (title is null)
                {
                    continue;
                }

                _logger.LogInformation("Renaming {OldName} to {NewName}", item.Name, title);
                item.Name = title;
                item.ForcedSortName = title;
                item.LockedFields = [.. item.LockedFields, MetadataField.Name];
                await item.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
                renamed++;
            }

            return renamed;
        }
        finally
        {
            _passLock.Release();
        }
    }

    private static bool IsCandidate(BaseItem item)
    {
        return item is Movie or Series
            && !item.IsLocked
            && !item.LockedFields.Contains(MetadataField.Name)
            && !string.IsNullOrEmpty(item.GetProviderId(MetadataProvider.Imdb))
            && TitleRules.HasIndicScript(item.OriginalTitle);
    }

    private void OnItemUpdated(object? sender, ItemChangeEventArgs e)
    {
        if (!IsCandidate(e.Item))
        {
            return;
        }

        lock (_queueLock)
        {
            _queuedItemIds.Add(e.Item.Id);
        }

        if (!_disposed)
        {
            try
            {
                _timer.Change(QuietPeriodMs, Timeout.Infinite);
            }
            catch (ObjectDisposedException)
            {
                // Raced with Dispose, nothing left to do
            }
        }
    }

    private void OnTimerElapsed()
    {
        // Fire and forget: anything escaping a timer callback would take the server down.
        _ = ProcessQueueAsync();
    }

    private async Task ProcessQueueAsync()
    {
        try
        {
            Guid[] ids;
            lock (_queueLock)
            {
                ids = [.. _queuedItemIds];
                _queuedItemIds.Clear();
            }

            var items = ids.Select(_libraryManager.GetItemById).OfType<BaseItem>().ToList();
            await ApplyAsync(items, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not apply romanized titles");
        }
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemAdded += OnItemUpdated;
        _libraryManager.ItemUpdated += OnItemUpdated;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemAdded -= OnItemUpdated;
        _libraryManager.ItemUpdated -= OnItemUpdated;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
        _timer.Dispose();
        _passLock.Dispose();
    }
}
