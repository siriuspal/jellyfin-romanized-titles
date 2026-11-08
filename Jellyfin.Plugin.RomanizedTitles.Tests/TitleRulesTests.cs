using Xunit;

namespace Jellyfin.Plugin.RomanizedTitles.Tests;

// Rows and titles below are copied from IMDb's title.basics.tsv and Jellyfin's TMDB metadata (2026-11-08).
public class TitleRulesTests
{
    [Theory]
    [InlineData("tt0055508\tmovie\tThree Daughters\tTeen Kanya\t0\t1961\t\\N\t114\tComedy,Drama,Fantasy", "tt0055508", "Teen Kanya")]
    [InlineData("tt0476884\tmovie\tTaxi No. 9 2 11: Nau Do Gyarah\tTaxi No. 9 2 11: Nau Do Gyarah\t0\t2006\t\\N\t116\tComedy,Drama,Thriller", "tt0476884", "Taxi No. 9 2 11: Nau Do Gyarah")]
    [InlineData("tt0053504\ttvSeries\tFull Circle\tFull Circle\t0\t1960\t1961\t30\tDrama", "tt0053504", "Full Circle")]
    public void ParsesKeptRows(string line, string expectedId, string expectedTitle)
    {
        Assert.True(TitleRules.TryParseBasicsRow(line, out var id, out var title));
        Assert.Equal(expectedId, id);
        Assert.Equal(expectedTitle, title);
    }

    [Theory]
    [InlineData("tconst\ttitleType\tprimaryTitle\toriginalTitle\tisAdult\tstartYear\tendYear\truntimeMinutes\tgenres")]
    [InlineData("tt17066206\ttvEpisode\tMan-Made Monster\tMan-Made Monster\t0\t1968\t\\N\t\\N\tDrama,Fantasy,Horror")]
    [InlineData("tt0000001\tshort\tCarmencita\tCarmencita\t0\t1894\t\\N\t1\tDocumentary,Short")]
    [InlineData("")]
    public void SkipsOtherRows(string line)
    {
        Assert.False(TitleRules.TryParseBasicsRow(line, out _, out _));
    }

    [Theory]
    [InlineData("Three Daughters", "তিন কন্যা", "Teen Kanya", "Teen Kanya")]
    [InlineData("The Chess Players", "शतरंज के खिलाड़ी", "Shatranj Ke Khilari", "Shatranj Ke Khilari")]
    [InlineData("Taxi No. 9211", "टैक्सी नम्बर ९२११", "Taxi No. 9 2 11: Nau Do Gyarah", "Taxi No. 9 2 11: Nau Do Gyarah")]
    public void RenamesIndianTitles(string name, string tmdbOriginal, string imdbOriginal, string expected)
    {
        Assert.Equal(expected, TitleRules.PickTitle(name, tmdbOriginal, imdbOriginal));
    }

    [Theory]
    [InlineData("Snowpiercer", "설국열차", "Snowpiercer")] // Korean: not an Indian script
    [InlineData("The Adversary", "The Adversary", "The Adversary")] // TMDB's original title is English
    [InlineData("Dil Chahta Hai", "दिल चाहता है", "Dil Chahta Hai")] // already right
    [InlineData("Devi", "দেবী", null)] // IMDb doesn't know it
    [InlineData("Devi", "দেবী", "দেবী")] // IMDb only has native script
    public void LeavesOthersAlone(string name, string tmdbOriginal, string? imdbOriginal)
    {
        Assert.Null(TitleRules.PickTitle(name, tmdbOriginal, imdbOriginal));
    }
}
