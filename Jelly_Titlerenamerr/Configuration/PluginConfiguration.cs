using System.Text.Json.Serialization;
using System.Xml.Serialization;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Jelly_Titlerenamerr.Configuration;

public enum YearOption
{
    RemoveFromTitle = 0,   // Default / Recommended: Year is saved to Jellyfin metadata, NOT in title
    ParenthesizeInTitle = 1, // e.g. "Movie (2026)"
    KeepOriginal = 2
}

public enum CasingOption
{
    Preserve = 0,
    TitleCase = 1
}

public enum TitleUpdateMode
{
    CleanIfDirty = 0,               // Recommended: updates if title equals raw filename or has dirty patterns
    OnlyIfTitleEqualsFilename = 1, // Safe mode: only updates if Jellyfin title == raw filename
    AlwaysClean = 2                // Force clean: unconditionally cleans title from filename or current title
}

public class PluginConfiguration : BasePluginConfiguration
{
    public PluginConfiguration()
    {
        ProcessAllLibraries = true;
        SelectedLibraryIds = new List<string>();
        EnableMovies = true;
        EnableEpisodes = true;
        RunOnItemAdded = true;
        RunOnItemUpdated = false;
        UpdateMode = 0;

        CustomKeywordsToRemove = "5MovieRulz\nMovieRulz\nTamilMV\nTamilBlasters\nCineVood\nBolly4u\nTorrentCounter";
        CustomRegexPatterns = string.Empty;

        RemoveWebsites = true;
        RemoveQualityTags = true;
        RemoveCodecs = true;
        RemoveLanguageTags = true;
        RemoveReleaseGroups = true;
        RemoveMiscJunk = true;
        ReplaceDotsWithSpaces = true;
        ReplaceUnderscoresWithSpaces = true;
        CleanPunctuationAndDashes = true;

        YearHandling = 0; // Default: Year in metadata, NOT in title (Requirement 3)
        CasingHandling = 0;
    }

    // === Library Selection ===
    public bool ProcessAllLibraries { get; set; }
    public List<string> SelectedLibraryIds { get; set; }

    // === Media Types ===
    public bool EnableMovies { get; set; }
    public bool EnableEpisodes { get; set; }

    // === Automation Triggers ===
    public bool RunOnItemAdded { get; set; }
    public bool RunOnItemUpdated { get; set; }

    // Int properties for 100% seamless JSON/XML serialization across all Jellyfin versions
    public int UpdateMode { get; set; }

    // === Custom Removals ===
    public string CustomKeywordsToRemove { get; set; }
    public string CustomRegexPatterns { get; set; }

    // === Cleaning Options ===
    public bool RemoveWebsites { get; set; }
    public bool RemoveQualityTags { get; set; }
    public bool RemoveCodecs { get; set; }
    public bool RemoveLanguageTags { get; set; }
    public bool RemoveReleaseGroups { get; set; }
    public bool RemoveMiscJunk { get; set; }
    public bool ReplaceDotsWithSpaces { get; set; }
    public bool ReplaceUnderscoresWithSpaces { get; set; }
    public bool CleanPunctuationAndDashes { get; set; }

    // === Formatting ===
    public int YearHandling { get; set; }
    public int CasingHandling { get; set; }

    [XmlIgnore]
    [JsonIgnore]
    public TitleUpdateMode UpdateModeEnum => (TitleUpdateMode)UpdateMode;

    [XmlIgnore]
    [JsonIgnore]
    public YearOption YearHandlingEnum => (YearOption)YearHandling;

    [XmlIgnore]
    [JsonIgnore]
    public CasingOption CasingHandlingEnum => (CasingOption)CasingHandling;
}
