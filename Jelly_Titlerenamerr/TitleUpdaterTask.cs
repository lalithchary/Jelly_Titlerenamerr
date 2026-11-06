using Jellyfin.Data.Enums;
using Jellyfin.Plugin.Jelly_Titlerenamerr.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Jelly_Titlerenamerr;

public class TitleUpdaterTask : IScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly FilenameCleanerService _cleaner;
    private readonly ILogger<TitleUpdaterTask> _logger;

    public TitleUpdaterTask(
        ILibraryManager libraryManager,
        FilenameCleanerService cleaner,
        ILogger<TitleUpdaterTask> logger)
    {
        _libraryManager = libraryManager;
        _cleaner = cleaner;
        _logger = logger;
    }

    public string Name => "Jelly_Titlerenamerr: Clean Titles & Extract Year";
    public string Key => "Jelly_TitlerenamerrScheduledTask";
    public string Description => "Cleans media item titles and extracts release year into Jellyfin metadata according to Jelly_Titlerenamerr configuration.";
    public string Category => "Library";

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => [];

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();

        var targetKinds = new List<BaseItemKind>();
        if (config.EnableMovies) targetKinds.Add(BaseItemKind.Movie);
        if (config.EnableEpisodes) targetKinds.Add(BaseItemKind.Episode);
        if (targetKinds.Count == 0)
        {
            targetKinds.Add(BaseItemKind.Movie);
            targetKinds.Add(BaseItemKind.Episode);
        }

        var items = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IsFolder = false,
            Recursive = true,
            IncludeItemTypes = targetKinds.ToArray()
        });

        // Filter by selected libraries if not processing all
        if (!config.ProcessAllLibraries && config.SelectedLibraryIds is not null && config.SelectedLibraryIds.Count > 0)
        {
            items = items.Where(item => Plugin.Instance?.BelongsToSelectedLibraries(item) ?? true).ToList();
        }

        var total = items.Count;
        if (total == 0)
        {
            progress.Report(100);
            return;
        }

        for (int i = 0; i < total; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var item = items[i];

            try
            {
                if (!string.IsNullOrEmpty(item.Path))
                {
                    var rawName = Path.GetFileNameWithoutExtension(item.Path);
                    var isRawName = string.Equals(item.Name, rawName, StringComparison.OrdinalIgnoreCase);

                    var shouldProcess = config.UpdateModeEnum switch
                    {
                        TitleUpdateMode.OnlyIfTitleEqualsFilename => isRawName,
                        TitleUpdateMode.CleanIfDirty => isRawName || _cleaner.IsDirty(item.Name, config),
                        TitleUpdateMode.AlwaysClean => true,
                        _ => isRawName
                    };

                    if (shouldProcess)
                    {
                        var cleanResult = _cleaner.CleanWithMetadata(item.Path, config);
                        if (string.IsNullOrEmpty(cleanResult.Title))
                        {
                            cleanResult = _cleaner.CleanWithMetadata(item.Name, config);
                        }

                        if (!string.IsNullOrEmpty(cleanResult.Title))
                        {
                            bool hasChanges = false;

                            // Title update (no year in title)
                            if (!string.Equals(item.Name, cleanResult.Title, StringComparison.Ordinal))
                            {
                                _logger.LogInformation(
                                    "[Jelly_Titlerenamerr] (ScheduledTask) Title: \"{OldTitle}\" → \"{NewTitle}\" (File: {File})",
                                    item.Name,
                                    cleanResult.Title,
                                    Path.GetFileName(item.Path));

                                item.Name = cleanResult.Title;
                                hasChanges = true;
                            }

                            // Year in metadata
                            if (cleanResult.Year.HasValue)
                            {
                                if (!item.ProductionYear.HasValue || item.ProductionYear.Value != cleanResult.Year.Value)
                                {
                                    _logger.LogInformation(
                                        "[Jelly_Titlerenamerr] (ScheduledTask) Year: {OldYear} → {NewYear} for \"{Title}\"",
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
                                await _libraryManager
                                    .UpdateItemAsync(item, item.GetParent(), ItemUpdateType.MetadataEdit, cancellationToken)
                                    .ConfigureAwait(false);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Jelly_Titlerenamerr] Failed to update title for item {ItemId}", item.Id);
            }

            progress.Report((double)(i + 1) / total * 100);
        }
    }
}
