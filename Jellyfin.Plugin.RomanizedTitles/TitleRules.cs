using System;

namespace Jellyfin.Plugin.RomanizedTitles;

/// <summary>
/// The rules for deciding a title, kept free of Jellyfin types so they can be tested on their own.
/// </summary>
public static class TitleRules
{
    /// <summary>
    /// IMDb title types worth keeping from title.basics (episodes, shorts and games are left out).
    /// </summary>
    private static readonly string[] _keptTitleTypes = ["movie", "tvMovie", "tvSeries", "tvMiniSeries", "tvSpecial", "video"];

    /// <summary>
    /// Whether the text contains an Indian script: Devanagari through Malayalam
    /// (Hindi, Marathi, Bengali, Punjabi, Gujarati, Odia, Tamil, Telugu, Kannada, Malayalam).
    /// </summary>
    /// <param name="text">The text, usually TMDB's original title.</param>
    /// <returns><c>true</c> if any character is in those blocks.</returns>
    public static bool HasIndicScript(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        foreach (var c in text)
        {
            if (c >= 'ऀ' && c <= 'ൿ')
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reads one row of IMDb's title.basics.tsv: tconst, titleType, primaryTitle, originalTitle, ….
    /// </summary>
    /// <param name="line">The row.</param>
    /// <param name="imdbId">The tconst, e.g. tt0055508.</param>
    /// <param name="originalTitle">IMDb's original title, e.g. Teen Kanya.</param>
    /// <returns><c>true</c> for a kept title type with a usable original title.</returns>
    public static bool TryParseBasicsRow(string line, out string imdbId, out string originalTitle)
    {
        imdbId = string.Empty;
        originalTitle = string.Empty;

        var fields = line.Split('\t', 5);
        if (fields.Length < 4 || !fields[0].StartsWith("tt", StringComparison.Ordinal))
        {
            return false;
        }

        if (Array.IndexOf(_keptTitleTypes, fields[1]) < 0 || fields[3] == "\\N" || fields[3].Length == 0)
        {
            return false;
        }

        imdbId = fields[0];
        originalTitle = fields[3];
        return true;
    }

    /// <summary>
    /// Decides the new title for an item.
    /// </summary>
    /// <param name="currentName">The item's current name.</param>
    /// <param name="tmdbOriginalTitle">The original title Jellyfin has (from TMDB, in native script).</param>
    /// <param name="imdbOriginalTitle">IMDb's original title, if known.</param>
    /// <returns>The new title, or <c>null</c> to leave the item alone.</returns>
    public static string? PickTitle(string? currentName, string? tmdbOriginalTitle, string? imdbOriginalTitle)
    {
        if (!HasIndicScript(tmdbOriginalTitle)
            || string.IsNullOrWhiteSpace(imdbOriginalTitle)
            || HasIndicScript(imdbOriginalTitle)
            || string.Equals(currentName, imdbOriginalTitle, StringComparison.Ordinal))
        {
            return null;
        }

        return imdbOriginalTitle.Trim();
    }
}
