using Jellyfin.Plugin.Jelly_Titlerenamerr;
using Jellyfin.Plugin.Jelly_Titlerenamerr.Configuration;
using Xunit;

namespace Jelly_Titlerenamerr.Tests;

public class FilenameCleanerServiceTests
{
    private readonly FilenameCleanerService _sut = new();

    [Theory]
    // User's exact requested case: Title without year, year extracted separately
    [InlineData(
        "www.5MovieRulz.company - Lalithchary (2026) 1080p Telugu HQ HDRip - HEVC - (DD+5.1 - 192kbps & AAC) - 2.1GB - ESub.mkv",
        "Lalithchary",
        2026)]
    // Website prefixes with domain changes
    [InlineData(" www.5MovieRulz.company - I'm Game", "I'm Game", null)]
    [InlineData("www.5MovieRulz.company - I'm Game.mkv", "I'm Game", null)]
    [InlineData("[www.5MovieRulz.company] - I'm Game (2024) [1080p].mkv", "I'm Game", 2024)]
    [InlineData("[1TamilMV.cz] I'm Game (2024) Telugu True WEB-DL - 1080p - AVC - (DD+5.1 - 384Kbps) [1.8GB] - ESub.mkv", "I'm Game", 2024)]
    [InlineData("[TamilBlasters.vip] - I'm Game (2024) [Multi Audio] [Hindi - Tam - Tel] - 720p - HDRip - x264.mkv", "I'm Game", 2024)]
    [InlineData("www.1TamilMV.lat - I'm Game (2024) 1080p HD AVC.mp4", "I'm Game", 2024)]
    [InlineData("CineVood.net - I'm Game (2024) Dual Audio 480p HDRip.mkv", "I'm Game", 2024)]
    [InlineData("5MovieRulz.company - The Dark Knight (2008) 720p.mkv", "The Dark Knight", 2008)]
    [InlineData("www.TorrentCounter.lat - Inception (2010) - 1080p - WEB-DL.mkv", "Inception", 2010)]
    // Scene and TV releases
    [InlineData("The.Dark.Knight.mkv", "The Dark Knight", null)]
    [InlineData("Breaking_Bad_S01E01.mp4", "Breaking Bad S01E01", null)]
    [InlineData("The.Office.S03E05.720p.WEB-DL.AAC.x264.mp4", "The Office S03E05", null)]
    [InlineData("Inception.2010.4K.UHD.BluRay.HEVC.TrueHD.Atmos.mkv", "Inception", 2010)]
    [InlineData("Movie.2024.1080p.BluRay.x264.mkv", "Movie", 2024)]
    [InlineData("www.EzTvX.to_The.Dark.Knight.2008.1080p.BluRay.x264.mkv", "The Dark Knight", 2008)]
    [InlineData("The.Dark.Knight", "The Dark Knight", null)]
    public void CleanWithMetadata_ReturnsExpectedTitleAndYear(string input, string expectedTitle, int? expectedYear)
    {
        var result = _sut.CleanWithMetadata(input);
        Assert.Equal(expectedTitle, result.Title);
        Assert.Equal(expectedYear, result.Year);
    }

    [Fact]
    public void Clean_ExtensionOnly_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, _sut.Clean(".mkv"));
    }

    [Fact]
    public void Clean_YearInParentheses_ExtractsYearAndRemovesFromTitle()
    {
        var res = _sut.CleanWithMetadata("Interstellar.(2014).mkv");
        Assert.Equal("Interstellar", res.Title);
        Assert.Equal(2014, res.Year);
    }

    [Fact]
    public void Clean_YearInSquareBrackets_ExtractsYearAndRemovesFromTitle()
    {
        var res = _sut.CleanWithMetadata("Dune.[2021].2160p.mkv");
        Assert.Equal("Dune", res.Title);
        Assert.Equal(2021, res.Year);
    }

    [Fact]
    public void Clean_MultipleSpacesCollapsed()
    {
        Assert.Equal("Some Movie", _sut.Clean("Some...Movie.mkv"));
    }

    [Fact]
    public void Clean_CustomKeyword_RemovesKeyword()
    {
        var config = new PluginConfiguration
        {
            CustomKeywordsToRemove = "SpecialLeakSite\nMySecretClub"
        };

        var res = _sut.CleanWithMetadata("SpecialLeakSite - Avatar 2 (2022).mkv", config);
        Assert.Equal("Avatar 2", res.Title);
        Assert.Equal(2022, res.Year);
    }

    [Fact]
    public void Clean_CustomRegex_RemovesMatchingPattern()
    {
        var config = new PluginConfiguration
        {
            CustomRegexPatterns = @"\[ReleaseGroup\d+\]"
        };

        var res = _sut.CleanWithMetadata("[ReleaseGroup99] - Oppenheimer (2023).mkv", config);
        Assert.Equal("Oppenheimer", res.Title);
        Assert.Equal(2023, res.Year);
    }

    [Fact]
    public void Clean_YearOptionParenthesize_WrapsYearInTitle()
    {
        var config = new PluginConfiguration
        {
            YearHandling = (int)YearOption.ParenthesizeInTitle
        };

        var res = _sut.CleanWithMetadata("Gladiator.(2000).1080p.mkv", config);
        Assert.Equal("Gladiator (2000)", res.Title);
        Assert.Equal(2000, res.Year);
    }

    [Fact]
    public void IsDirty_DetectsDirtyTitles()
    {
        Assert.True(_sut.IsDirty(" www.5MovieRulz.company - I'm Game"));
        Assert.True(_sut.IsDirty("The Dark Knight 1080p BluRay"));
        Assert.True(_sut.IsDirty("Inception [TamilBlasters]"));
        Assert.False(_sut.IsDirty("The Dark Knight"));
        Assert.False(_sut.IsDirty("I'm Game"));
    }
}
