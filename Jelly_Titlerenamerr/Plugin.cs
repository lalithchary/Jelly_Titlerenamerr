using Jellyfin.Data.Enums;
using Jellyfin.Plugin.Jelly_Titlerenamerr.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Jelly_Titlerenamerr;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages, IDisposable
{
    private readonly ILibraryManager _libraryManager;
    private readonly FilenameCleanerService _cleaner;
    private readonly ILogger<Plugin> _logger;

    public static Plugin? Instance { get; private set; }

    public Plugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer,
        ILibraryManager libraryManager,
        ILogger<Plugin> logger)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        _libraryManager = libraryManager;
        _cleaner = new FilenameCleanerService();
        _logger = logger;

        _libraryManager.ItemAdded += OnItemAdded;
        _libraryManager.ItemUpdated += OnItemUpdated;
    }

    public override string Name => "Jelly_Titlerenamerr";

    public override Guid Id => Guid.Parse("3f2a1b4c-5d6e-7f8a-9b0c-1d2e3f4a5b6c");

    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = this.Name,
                EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configPage.html",
                EnableInMainMenu = true,
                DisplayName = "Jelly_Titlerenamerr"
            }
        };
    }

    private void OnItemAdded(object? sender, ItemChangeEventArgs args)
    {
        if (!Configuration.RunOnItemAdded)
        {
            return;
        }

        ProcessItem(args.Item, "ItemAdded");
    }

    private void OnItemUpdated(object? sender, ItemChangeEventArgs args)
    {
        if (!Configuration.RunOnItemUpdated)
        {
            return;
        }

        // Prevent recursive loops if the update was triggered by our own metadata edit
        if (args.UpdateReason == ItemUpdateType.MetadataEdit)
        {
            return;
        }

        ProcessItem(args.Item, "ItemUpdated");
    }

    public void ProcessItem(BaseItem? item, string eventSource)
    {
        if (item is null || string.IsNullOrEmpty(item.Path))
        {
            return;
        }

        // 1. Media Type Filter
        var kind = item.GetBaseItemKind();
        var isMovie = kind == BaseItemKind.Movie;
        var isEpisode = kind == BaseItemKind.Episode;

        if (!isMovie && !isEpisode)
        {
            return;
        }

        if (isMovie && !Configuration.EnableMovies)
        {
            return;
        }

        if (isEpisode && !Configuration.EnableEpisodes)
        {
            return;
        }

        // 2. Library Selection Filter
        if (!BelongsToSelectedLibraries(item))
        {
            return;
        }

        // 3. Update Mode Filter
        var rawName = Path.GetFileNameWithoutExtension(item.Path);
        var isRawName = string.Equals(item.Name, rawName, StringComparison.OrdinalIgnoreCase);

        switch (Configuration.UpdateModeEnum)
        {
            case TitleUpdateMode.OnlyIfTitleEqualsFilename:
                if (!isRawName)
                {
                    return;
                }
                break;

            case TitleUpdateMode.CleanIfDirty:
                var isDirty = _cleaner.IsDirty(item.Name, Configuration);
                if (!isRawName && !isDirty)
                {
                    return;
                }
                break;

            case TitleUpdateMode.AlwaysClean:
                // Always clean
                break;
        }

        // 4. Calculate clean title and metadata year
        var cleanResult = _cleaner.CleanWithMetadata(item.Path, Configuration);
        if (string.IsNullOrEmpty(cleanResult.Title))
        {
            cleanResult = _cleaner.CleanWithMetadata(item.Name, Configuration);
        }

        if (string.IsNullOrEmpty(cleanResult.Title))
        {
            return;
        }

        var hasChanges = false;

        // Requirement 3: Clean title (without year by default)
        if (!string.Equals(item.Name, cleanResult.Title, StringComparison.Ordinal))
        {
            _logger.LogInformation(
                "[Jelly_Titlerenamerr] ({Event}) Title: \"{OldTitle}\" → \"{NewTitle}\" (File: {File})",
                eventSource,
                item.Name,
                cleanResult.Title,
                Path.GetFileName(item.Path));

            item.Name = cleanResult.Title;
            hasChanges = true;
        }

        // Requirement 3: Year should be in metadata
        if (cleanResult.Year.HasValue)
        {
            if (!item.ProductionYear.HasValue || item.ProductionYear.Value != cleanResult.Year.Value)
            {
                _logger.LogInformation(
                    "[Jelly_Titlerenamerr] ({Event}) Year: {OldYear} → {NewYear} for \"{Title}\"",
                    eventSource,
                    item.ProductionYear,
                    cleanResult.Year.Value,
                    item.Name);

                item.ProductionYear = cleanResult.Year.Value;
                if (!item.PremiereDate.HasValue)
                {
                    item.PremiereDate = new DateTime(cleanResult.Year.Value, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                }

                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            _ = _libraryManager.UpdateItemAsync(
                item,
                item.GetParent(),
                ItemUpdateType.MetadataEdit,
                CancellationToken.None);
        }
    }

    public bool BelongsToSelectedLibraries(BaseItem item)
    {
        var config = Configuration;
        if (config.ProcessAllLibraries || config.SelectedLibraryIds is null || config.SelectedLibraryIds.Count == 0)
        {
            return true;
        }

        // 1. Check collection folders from LibraryManager
        try
        {
            var collectionFolders = _libraryManager.GetCollectionFolders(item);
            foreach (var folder in collectionFolders)
            {
                var fIdStr = folder.Id.ToString("N");
                var fIdDashes = folder.Id.ToString();
                if (config.SelectedLibraryIds.Any(id =>
                    string.Equals(id, fIdStr, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(id, fIdDashes, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(id, folder.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
        }
        catch
        {
            // Fallback to parent traversal below
        }

        // 2. Check parent folder hierarchy
        var parent = item.GetParent();
        while (parent != null)
        {
            var pIdStr = parent.Id.ToString("N");
            var pIdDashes = parent.Id.ToString();
            if (config.SelectedLibraryIds.Any(id =>
                string.Equals(id, pIdStr, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(id, pIdDashes, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(id, parent.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            parent = parent.GetParent();
        }

        return false;
    }

    public void Dispose()
    {
        _libraryManager.ItemAdded -= OnItemAdded;
        _libraryManager.ItemUpdated -= OnItemUpdated;
        GC.SuppressFinalize(this);
    }
}
