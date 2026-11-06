using System.Globalization;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.Jelly_Titlerenamerr.Configuration;

namespace Jellyfin.Plugin.Jelly_Titlerenamerr;

public record CleanResult(string Title, int? Year)
{
    public override string ToString() => Title;
}

public class FilenameCleanerService
{
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mkv", ".mp4", ".avi", ".mov", ".wmv", ".flv", ".m4v",
        ".ts", ".m2ts", ".webm", ".mpg", ".mpeg", ".vob", ".iso", ".3gp"
    };

    // Common TLDs used by media and torrent sites (ordered by length descending so longer TLDs match before sub-parts)
    private const string TldPattern =
        "company|online|website|click|store|link|site|live|info|club|tech|pro|biz|com|net|org|io|tv|to|co|in|cc|ws|me|top|xyz|lat|vip|icu|pw|cz|pl|pm|vc|eu|uk|us|de|ru|ch|nl|se|es|cat|group|fun";

    // Primary rule:
    // Extract "after first '-' to end of the ')' which contains the year"
    // e.g. "www.5MovieRulz.company - Lalithchary (2026) 1080p Telugu HQ HDRip - HEVC - (DD+5.1 - 192kbps & AAC) - 2.1GB - ESub.mkv"
    // -> Title: "Lalithchary", Year: 2026
    private static readonly Regex AfterFirstHyphenToYearRegex = new(
        @"^.*?[-–—]\s*(?<title>.+?)\s*[\(\[](?<year>(?:19|20)\d{2})[\)\]]",
        RegexOptions.Compiled);

    // Bracketed website/tag prefix before title and year:
    // e.g. "[1TamilMV.cz] I'm Game (2024) Telugu True WEB-DL ..."
    private static readonly Regex BracketPrefixToYearRegex = new(
        @"^\[[^\]]+\]\s*[-–—]?\s*(?<title>.+?)\s*[\(\[](?<year>(?:19|20)\d{2})[\)\]]",
        RegexOptions.Compiled);

    // Domain prefix before title and year:
    // e.g. "5MovieRulz.company Lalithchary (2026) 1080p ..."
    private static readonly Regex DomainPrefixToYearRegex = new(
        @"^(?:(?:https?:\/\/|www\.)?[a-zA-Z0-9_\-\.]+\.(?:" + TldPattern + @"))\s*[-–—]?\s*(?<title>.+?)\s*[\(\[](?<year>(?:19|20)\d{2})[\)\]]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Standalone title followed by parenthesized or bracketed year:
    // e.g. "Interstellar.(2014).1080p.mkv" -> Title: "Interstellar", Year: 2014
    private static readonly Regex StandaloneTitleToYearRegex = new(
        @"^(?<title>[^(\[]+?)\s*[\(\[](?<year>(?:19|20)\d{2})[\)\]]",
        RegexOptions.Compiled);

    // Title with dots/spaces followed by standalone year:
    // e.g. "The.Dark.Knight.2008.1080p.BluRay.x264.mkv" -> Title: "The Dark Knight", Year: 2008
    private static readonly Regex DotSeparatedTitleToYearRegex = new(
        @"^(?<title>[a-zA-Z0-9\s\._\-'’]+?)[\.\s_-]+(?<year>(?:19|20)\d{2})(?:[\.\s_-]|$)",
        RegexOptions.Compiled);

    // Matches website prefixes at start of string (e.g. "www.5MovieRulz.company - ", "[www.5MovieRulz.company] - ")
    private static readonly Regex LeadingWebsitePrefixRegex = new(
        @"^(?:[\[\(]?(?:https?:\/\/|www\.)?[a-zA-Z0-9_\-\.]+\.(?:" + TldPattern + @")[\]\)]?[\s\-_:~|]*)+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Matches bracketed or parenthesized URLs/domains anywhere
    private static readonly Regex BracketedDomainRegex = new(
        @"[\[\(][\s]*(?:https?:\/\/|www\.)?[a-zA-Z0-9_\-\.]+\.(?:" + TldPattern + @")[^\]\)]*[\]\)][\s\-_:~|]*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Standalone URLs or www.*
    private static readonly Regex WwwOrUrlRegex = new(
        @"(?:https?:\/\/|www\.)[a-zA-Z0-9_\-\.]+\.[a-zA-Z]{2,}(?::\d+)?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Standalone domain names matching TLD list
    private static readonly Regex KnownTldDomainRegex = new(
        @"\b[a-zA-Z0-9_\-]+\.(?:" + TldPattern + @")\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Common piracy/scene site names without TLD
    private static readonly Regex KnownSitesPrefixRegex = new(
        @"^(?:\[\s*)?(?:5MovieRulz|MovieRulz|1TamilMV|TamilMV|TamilBlasters|CineVood|Bolly4u|TorrentCounter|YTS(?:\.MX)?|YIFY|PSA|TGx|Pahe|GalaxyRG|RARBG|mkvCinemas)(?:\s*\])?[\s\-_:~|]*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex KnownSitesBracketedRegex = new(
        @"\[\s*(?:5MovieRulz|MovieRulz|1TamilMV|TamilMV|TamilBlasters|CineVood|Bolly4u|TorrentCounter|YTS(?:\.MX)?|YIFY|PSA|TGx|Pahe|GalaxyRG|RARBG|mkvCinemas)\s*\][\s\-_:~|]*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // File size tags (e.g. 2.1GB, [1.8GB], (700MB))
    private static readonly Regex FileSizeRegex = new(
        @"(?:[-–—]\s*)?[\[\(]?\s*\d+(?:\.\d+)?\s*(?:GB|MB|GiB|MiB)\s*[\]\)]?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Audio specs in brackets/parentheses e.g. (DD+5.1 - 192kbps & AAC)
    private static readonly Regex AudioSpecsRegex = new(
        @"[\[\(][^\]\)]*(?:5\.1|7\.1|2\.0|6CH|8CH|2CH|Kbps|DD\+|DDP|Dolby|AAC|AC3|Atmos)[^\]\)]*[\]\)]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Standalone audio channel specs e.g. DD+5.1, DDP5.1, 192kbps
    private static readonly Regex StandaloneAudioSpecsRegex = new(
        @"\b(?:DD\+?5\.1|DDP5\.1|5\.1ch|7\.1ch|2\.0ch|\d+\s*Kbps)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Language / Audio brackets e.g. [Hindi + Telugu], [Multi Audio], [Hindi - Tam - Tel]
    private static readonly Regex MultiLanguageBracketRegex = new(
        @"\[[\s\w\+\-\/\&]*(?:Hindi|Tamil|Telugu|Malayalam|Kannada|Bengali|Marathi|Punjabi|Eng|English|Hin|Tam|Tel|Mal|Kan)[\s\w\+\-\/\&]*\][\s\-_:~|]*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DualMultiAudioRegex = new(
        @"(?:\[\s*)?\b(?:Multi[\s\-]Audio|Dual[\s\-]Audio)\b(?:\s*\])?[\s\-_:~|]*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SubtitleTagsRegex = new(
        @"(?:\[\s*)?(?:ESub|ESubs|Soft[\s\-]ESub|MSubs|English Subtitles|Eng Sub|Subbed)(?:\s*\])?[\s\-_:~|]*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Quality, Codecs, and Junk tokens
    private static readonly string[] QualityTags =
    [
        "2160p", "1080p", "1080i", "720p", "576p", "480p", "360p",
        "4K", "8K", "UHD", "FHD", "HD", "HQ",
        "BluRay", "BDRip", "BDRemux", "BRRip", "Remux",
        "WEB-DL", "WEBRip", "WEBDL", "WEB", "HDRip", "HD-Rip",
        "HDTV", "PDTV", "DSRip", "TVRip",
        "DVDRip", "DVDScr", "DVD", "DVD5", "DVD9", "R5",
        "PreDVDRip", "Pre-DVDRip", "PreDVD", "CAMRip", "HDCAM", "CAM", "HDTS", "TS", "TELESYNC", "Screener"
    ];

    private static readonly string[] CodecTags =
    [
        "x264", "x265", "H264", "H265", "HEVC", "AVC", "AV1", "VC1", "XviD", "DivX",
        "10bit", "8bit", "12bit",
        "AAC", "AC3", "EAC3", "DTS-HD", "DTS-MA", "DTS", "MP3", "TrueHD", "Atmos", "FLAC", "Opus",
        "HDR10+", "HDR10Plus", "HDR10", "HDR", "SDR", "DV", "DoVi", "Dolby Vision", "DolbyVision", "IMAX"
    ];

    private static readonly string[] MiscJunkTags =
    [
        "PROPER", "REPACK", "REAL", "RERIP", "EXTENDED", "THEATRICAL", "UNRATED", "UNCUT",
        "DIRECTORS CUT", "DIRECTOR'S CUT", "SPECIAL EDITION", "ANNIVERSARY EDITION", "ANNIVERSARY",
        "REMASTERED", "RESTORED", "CRITERION", "COMPLETE", "INTERNAL", "LIMITED", "FESTIVAL",
        "Original Audio", "Clean Audio", "Line Audio", "HQ Line Audio", "True WEB-DL", "True"
    ];

    // Release groups at the end
    private static readonly Regex ReleaseGroupSuffixRegex = new(
        @"-(?:SPARKS|YTS(?:\.MX)?|YIFY|RARBG|PSA|TGx|ETRG|ION10|GalaxyRG|mkvCinemas|CMRG|FGT|EVO|NTb|FLUX|SMURF|QxR|d3g|rartv)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Language specifier following a 4-digit year (e.g. "2024) Telugu True" -> "2024)")
    private static readonly Regex PostYearLanguageSpecsRegex = new(
        @"(?<=\b(?:19|20)\d{2}\b[\s\)\-_:~|]+)(?:Telugu|Tamil|Hindi|Malayalam|Kannada|English|Bengali|Marathi|Punjabi)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Year pattern in brackets: (2024) or [2024]
    private static readonly Regex BracketedYearRegex = new(
        @"[\(\[]+\s*((?:19|20)\d{2})\s*[\)\]]+",
        RegexOptions.Compiled);

    // Standalone year pattern
    private static readonly Regex StandaloneYearRegex = new(
        @"\b((?:19|20)\d{2})\b",
        RegexOptions.Compiled);

    private static readonly Regex EmptyBracketsRegex = new(
        @"[\[\(\{][\s\-_:~|&]*[\]\)\}]",
        RegexOptions.Compiled);

    private static readonly Regex DanglingSeparatorsRegex = new(
        @"(?:\s*[\-_:~|]\s*){2,}",
        RegexOptions.Compiled);

    private static readonly Regex LeadingTrailingPunctuationRegex = new(
        @"^[ \t\-_:~|\.\,\/\\]+|[ \t\-_:~|\.\,\/\\]+$",
        RegexOptions.Compiled);

    private static readonly Regex ExtraSpacesRegex = new(
        @"\s{2,}",
        RegexOptions.Compiled);

    public CleanResult CleanWithMetadata(string filenameOrTitle, PluginConfiguration? config = null)
    {
        if (string.IsNullOrWhiteSpace(filenameOrTitle))
        {
            return new CleanResult(string.Empty, null);
        }

        config ??= new PluginConfiguration();

        // Step 1: Strip extension if it's a known media file extension
        var ext = Path.GetExtension(filenameOrTitle);
        var name = VideoExtensions.Contains(ext)
            ? Path.GetFileNameWithoutExtension(filenameOrTitle)
            : filenameOrTitle;

        name = name.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return new CleanResult(string.Empty, null);
        }

        // Step 2: Extract year from pattern (primary rules)
        Match yearMatch = Match.Empty;
        var hasHyphen = name.Contains('-') || name.Contains('–') || name.Contains('—');
        var isTvEpisode = Regex.IsMatch(name, @"[sS]\d+[eE]\d+", RegexOptions.Compiled);

        if (hasHyphen)
        {
            yearMatch = AfterFirstHyphenToYearRegex.Match(name);
        }

        if (!yearMatch.Success && name.StartsWith('['))
        {
            yearMatch = BracketPrefixToYearRegex.Match(name);
        }

        if (!yearMatch.Success)
        {
            yearMatch = DomainPrefixToYearRegex.Match(name);
        }

        if (!yearMatch.Success && !isTvEpisode)
        {
            yearMatch = StandaloneTitleToYearRegex.Match(name);
        }

        if (!yearMatch.Success && !isTvEpisode && !hasHyphen)
        {
            var dotMatch = DotSeparatedTitleToYearRegex.Match(name);
            if (dotMatch.Success)
            {
                yearMatch = dotMatch;
            }
        }

        int? extractedYear = null;

        if (yearMatch.Success)
        {
            var rawExtractedTitle = yearMatch.Groups["title"].Value;
            var yearStr = yearMatch.Groups["year"].Value;
            if (int.TryParse(yearStr, out var parsedYear))
            {
                extractedYear = parsedYear;
            }

            // Strip leading domain prefix if present inside extracted title
            if (config.RemoveWebsites)
            {
                rawExtractedTitle = LeadingWebsitePrefixRegex.Replace(rawExtractedTitle, " ");
                rawExtractedTitle = BracketedDomainRegex.Replace(rawExtractedTitle, " ");
                rawExtractedTitle = KnownSitesPrefixRegex.Replace(rawExtractedTitle, " ");
                rawExtractedTitle = KnownSitesBracketedRegex.Replace(rawExtractedTitle, " ");
                rawExtractedTitle = WwwOrUrlRegex.Replace(rawExtractedTitle, " ");
                rawExtractedTitle = KnownTldDomainRegex.Replace(rawExtractedTitle, " ");
            }

            // Normalize title (replace dots/underscores with spaces)
            var cleanTitlePart = rawExtractedTitle.Replace('.', ' ').Replace('_', ' ');
            cleanTitlePart = ExtraSpacesRegex.Replace(cleanTitlePart, " ").Trim();
            cleanTitlePart = LeadingTrailingPunctuationRegex.Replace(cleanTitlePart, string.Empty).Trim();

            if (!string.IsNullOrEmpty(cleanTitlePart))
            {
                string resultTitle;
                switch (config.YearHandlingEnum)
                {
                    case YearOption.ParenthesizeInTitle:
                        resultTitle = $"{cleanTitlePart} ({extractedYear})";
                        break;
                    case YearOption.KeepOriginal:
                        resultTitle = $"{cleanTitlePart} ({extractedYear})";
                        break;
                    case YearOption.RemoveFromTitle:
                    default:
                        // Requirement 3: Year should be in metadata, but NOT in title
                        resultTitle = cleanTitlePart;
                        break;
                }

                if (config.CasingHandlingEnum == CasingOption.TitleCase)
                {
                    resultTitle = ToTitleCase(resultTitle);
                }

                return new CleanResult(resultTitle, extractedYear);
            }
        }

        // Step 3: Fallback cleaning when no early year match succeeded
        if (config.RemoveWebsites)
        {
            name = Regex.Replace(name, @"^(?:[\[\(]?[^\s\-–—]+(?:www|\." + TldPattern + @"|MovieRulz|Tamil|CineVood|Bolly4u)[^\s\-–—]*[\]\)]?)\s*[-–—]\s*", "", RegexOptions.IgnoreCase);
            name = LeadingWebsitePrefixRegex.Replace(name, " ");
            name = BracketedDomainRegex.Replace(name, " ");
            name = KnownSitesPrefixRegex.Replace(name, " ");
            name = KnownSitesBracketedRegex.Replace(name, " ");
            name = WwwOrUrlRegex.Replace(name, " ");
            name = KnownTldDomainRegex.Replace(name, " ");
        }

        // Custom Regex & Custom Keywords
        if (!string.IsNullOrWhiteSpace(config.CustomRegexPatterns))
        {
            var lines = config.CustomRegexPatterns.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var pattern = line.Trim();
                if (string.IsNullOrEmpty(pattern)) continue;
                try
                {
                    name = Regex.Replace(name, pattern, " ", RegexOptions.IgnoreCase);
                }
                catch
                {
                    // Ignore invalid user regex
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(config.CustomKeywordsToRemove))
        {
            var keywords = config.CustomKeywordsToRemove.Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var keyword in keywords)
            {
                var kw = keyword.Trim();
                if (string.IsNullOrEmpty(kw)) continue;

                name = Regex.Replace(name, @"\[\s*" + Regex.Escape(kw) + @"\s*\][\s\-_:~|]*", " ", RegexOptions.IgnoreCase);
                name = Regex.Replace(name, @"^" + Regex.Escape(kw) + @"[\s\-_:~|]+", " ", RegexOptions.IgnoreCase);
                name = Regex.Replace(name, @"\b" + Regex.Escape(kw) + @"\b", " ", RegexOptions.IgnoreCase);
            }
        }

        // Special dot/bracket patterns (file size, audio specs) BEFORE dot replacement
        if (config.RemoveLanguageTags)
        {
            name = FileSizeRegex.Replace(name, " ");
            name = AudioSpecsRegex.Replace(name, " ");
            name = StandaloneAudioSpecsRegex.Replace(name, " ");
            name = MultiLanguageBracketRegex.Replace(name, " ");
            name = DualMultiAudioRegex.Replace(name, " ");
            name = SubtitleTagsRegex.Replace(name, " ");
        }

        // Replace dots and underscores with spaces
        if (config.ReplaceDotsWithSpaces)
        {
            name = name.Replace('.', ' ');
        }
        if (config.ReplaceUnderscoresWithSpaces)
        {
            name = name.Replace('_', ' ');
        }

        // Post-year language tags removal
        if (config.RemoveLanguageTags)
        {
            name = PostYearLanguageSpecsRegex.Replace(name, " ");
        }

        // Quality & Codec tags removal
        if (config.RemoveQualityTags)
        {
            foreach (var tag in QualityTags)
            {
                name = Regex.Replace(name, $@"\b{Regex.Escape(tag)}\b", " ", RegexOptions.IgnoreCase);
            }
        }

        if (config.RemoveCodecs)
        {
            foreach (var tag in CodecTags)
            {
                name = Regex.Replace(name, $@"\b{Regex.Escape(tag)}\b", " ", RegexOptions.IgnoreCase);
            }
        }

        // Misc Junk tags removal
        if (config.RemoveMiscJunk)
        {
            foreach (var tag in MiscJunkTags)
            {
                name = Regex.Replace(name, $@"\b{Regex.Escape(tag)}\b", " ", RegexOptions.IgnoreCase);
            }
        }

        // Release Group suffixes removal
        if (config.RemoveReleaseGroups)
        {
            name = ReleaseGroupSuffixRegex.Replace(name, " ");
        }

        // Extract year if present in fallback string
        if (!isTvEpisode)
        {
            var bYearMatch = BracketedYearRegex.Match(name);
            if (bYearMatch.Success && int.TryParse(bYearMatch.Groups[1].Value, out var yVal1))
            {
                extractedYear = yVal1;
            }
            else
            {
                var sYearMatch = StandaloneYearRegex.Match(name);
                if (sYearMatch.Success && int.TryParse(sYearMatch.Groups[1].Value, out var yVal2))
                {
                    extractedYear = yVal2;
                }
            }
        }

        // Year Formatting
        switch (config.YearHandlingEnum)
        {
            case YearOption.RemoveFromTitle:
            default:
                // Strip year from title (Requirement 3: Year should be in metadata but not in title)
                name = BracketedYearRegex.Replace(name, " ");
                name = StandaloneYearRegex.Replace(name, " ");
                break;
            case YearOption.ParenthesizeInTitle:
                if (BracketedYearRegex.IsMatch(name))
                {
                    name = BracketedYearRegex.Replace(name, " ($1) ");
                }
                else if (StandaloneYearRegex.IsMatch(name))
                {
                    name = StandaloneYearRegex.Replace(name, " ($1) ");
                }
                break;
            case YearOption.KeepOriginal:
                name = BracketedYearRegex.Replace(name, " ($1) ");
                break;
        }

        // Punctuation and dangling separators cleanup
        if (config.CleanPunctuationAndDashes)
        {
            for (int i = 0; i < 3; i++)
            {
                name = EmptyBracketsRegex.Replace(name, " ");
            }

            name = DanglingSeparatorsRegex.Replace(name, " - ");
            name = ExtraSpacesRegex.Replace(name, " ").Trim();

            name = LeadingTrailingPunctuationRegex.Replace(name, string.Empty);
            name = LeadingTrailingPunctuationRegex.Replace(name, string.Empty);
        }

        // Title Casing
        if (config.CasingHandlingEnum == CasingOption.TitleCase)
        {
            name = ToTitleCase(name);
        }

        name = ExtraSpacesRegex.Replace(name, " ").Trim();
        return new CleanResult(name, extractedYear);
    }

    public string Clean(string filenameOrTitle, PluginConfiguration? config = null)
    {
        return CleanWithMetadata(filenameOrTitle, config).Title;
    }

    public int? ExtractYear(string filenameOrTitle, PluginConfiguration? config = null)
    {
        return CleanWithMetadata(filenameOrTitle, config).Year;
    }

    public bool IsDirty(string title, PluginConfiguration? config = null)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        config ??= new PluginConfiguration();

        if (LeadingWebsitePrefixRegex.IsMatch(title) ||
            BracketedDomainRegex.IsMatch(title) ||
            WwwOrUrlRegex.IsMatch(title) ||
            KnownTldDomainRegex.IsMatch(title) ||
            KnownSitesPrefixRegex.IsMatch(title) ||
            KnownSitesBracketedRegex.IsMatch(title))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(config.CustomKeywordsToRemove))
        {
            var keywords = config.CustomKeywordsToRemove.Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var kw in keywords)
            {
                var trimmed = kw.Trim();
                if (!string.IsNullOrEmpty(trimmed) && title.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        foreach (var tag in QualityTags)
        {
            if (Regex.IsMatch(title, $@"\b{Regex.Escape(tag)}\b", RegexOptions.IgnoreCase))
            {
                return true;
            }
        }

        if (MultiLanguageBracketRegex.IsMatch(title) || SubtitleTagsRegex.IsMatch(title) || FileSizeRegex.IsMatch(title))
        {
            return true;
        }

        var cleaned = Clean(title, config);
        return !string.Equals(title.Trim(), cleaned, StringComparison.OrdinalIgnoreCase);
    }

    private static string ToTitleCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var minorWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "the", "and", "but", "or", "for", "nor", "on", "at", "to", "from", "by", "in", "of", "with"
        };

        var textInfo = CultureInfo.CurrentCulture.TextInfo;
        for (int i = 0; i < words.Length; i++)
        {
            var word = words[i];
            if (i == 0 || i == words.Length - 1 || !minorWords.Contains(word))
            {
                words[i] = textInfo.ToTitleCase(word.ToLowerInvariant());
            }
            else
            {
                words[i] = word.ToLowerInvariant();
            }
        }

        return string.Join(" ", words);
    }
}
