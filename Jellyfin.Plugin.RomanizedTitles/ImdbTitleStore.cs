using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RomanizedTitles;

/// <summary>
/// Keeps a trimmed copy of IMDb's title.basics (id and original title only) in the plugin's data folder.
/// The list is streamed for each lookup instead of held in memory: it has over a million rows.
/// </summary>
public sealed class ImdbTitleStore : IDisposable
{
    private const string DatasetUrl = "https://datasets.imdbws.com/title.basics.tsv.gz";
    private const string FileName = "imdb-original-titles.tsv";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ImdbTitleStore> _logger;
    private readonly SemaphoreSlim _downloadLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="ImdbTitleStore"/> class.
    /// </summary>
    /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{ImdbTitleStore}"/> interface.</param>
    public ImdbTitleStore(IHttpClientFactory httpClientFactory, ILogger<ImdbTitleStore> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    private static string FilePath => Path.Combine(Plugin.Instance!.DataFolderPath, FileName);

    /// <summary>
    /// Gets a value indicating whether the title list has been downloaded.
    /// </summary>
    public static bool Exists => File.Exists(FilePath);

    /// <summary>
    /// Downloads IMDb's title list and keeps only the rows and columns the plugin needs.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the download.</returns>
    public async Task DownloadAsync(CancellationToken cancellationToken)
    {
        await _downloadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(Plugin.Instance!.DataFolderPath);
            var tempPath = FilePath + ".tmp";
            var kept = 0;

            var client = _httpClientFactory.CreateClient(NamedClient.Default);
            using (var response = await client.GetAsync(new Uri(DatasetUrl), HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using (body.ConfigureAwait(false))
                {
                    using var gzip = new GZipStream(body, CompressionMode.Decompress);
                    using var reader = new StreamReader(gzip, Encoding.UTF8);
                    var writer = new StreamWriter(tempPath, false, new UTF8Encoding(false));
                    await using (writer.ConfigureAwait(false))
                    {
                        string? line;
                        while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
                        {
                            if (TitleRules.TryParseBasicsRow(line, out var imdbId, out var originalTitle))
                            {
                                await writer.WriteLineAsync($"{imdbId}\t{originalTitle}".AsMemory(), cancellationToken).ConfigureAwait(false);
                                kept++;
                            }
                        }
                    }
                }
            }

            File.Move(tempPath, FilePath, true);
            _logger.LogInformation("IMDb title list updated: {Count} titles", kept);
        }
        finally
        {
            _downloadLock.Release();
        }
    }

    /// <summary>
    /// Looks up IMDb's original titles, downloading the list first if it isn't there yet.
    /// </summary>
    /// <param name="imdbIds">The IMDb ids to look up.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Original titles by IMDb id; ids IMDb doesn't know are missing.</returns>
    public async Task<Dictionary<string, string>> LookupAsync(IReadOnlySet<string> imdbIds, CancellationToken cancellationToken)
    {
        var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (imdbIds.Count == 0)
        {
            return found;
        }

        if (!Exists)
        {
            await DownloadAsync(cancellationToken).ConfigureAwait(false);
        }

        using var reader = new StreamReader(FilePath, Encoding.UTF8);
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
        {
            var tab = line.IndexOf('\t', StringComparison.Ordinal);
            if (tab > 0 && imdbIds.Contains(line[..tab]))
            {
                found[line[..tab]] = line[(tab + 1)..];
                if (found.Count == imdbIds.Count)
                {
                    break;
                }
            }
        }

        return found;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _downloadLock.Dispose();
    }
}
