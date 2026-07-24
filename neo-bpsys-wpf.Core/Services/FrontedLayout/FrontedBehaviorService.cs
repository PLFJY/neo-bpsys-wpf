using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Designer v3 前台布局的基于文件的行为文档服务。
/// </summary>
public sealed class FrontedBehaviorService : IFrontedBehaviorService
{
    private readonly IFrontedUserLayoutStore _userLayoutStore;
    private readonly IFrontedLayoutPackageManager _packageManager;
    private readonly ILogger<FrontedBehaviorService> _logger;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        MaxDepth = FrontedLayoutLimits.MaxJsonDepth
    };

    private FrontedBehaviorDocument? _currentDocument;

    public FrontedBehaviorService()
        : this(new FrontedUserLayoutStore(), new FrontedLayoutPackageManager(), NullLogger<FrontedBehaviorService>.Instance)
    {
    }

    public FrontedBehaviorService(
        IFrontedUserLayoutStore userLayoutStore,
        ILogger<FrontedBehaviorService> logger)
        : this(userLayoutStore, new FrontedLayoutPackageManager(), logger)
    {
    }

    public FrontedBehaviorService(
        IFrontedUserLayoutStore userLayoutStore,
        IFrontedLayoutPackageManager? packageManager,
        ILogger<FrontedBehaviorService>? logger)
    {
        _userLayoutStore = userLayoutStore;
        _packageManager = packageManager ?? new FrontedLayoutPackageManager();
        _logger = logger ?? NullLogger<FrontedBehaviorService>.Instance;
    }

    /// <inheritdoc />
    public async Task<FrontedBehaviorDocument> LoadDocumentAsync(
        string canonicalWindowId,
        CancellationToken cancellationToken = default)
    {
        var path = await ResolveLoadPathAsync(canonicalWindowId, cancellationToken);
        _currentDocument = await LoadDocumentFromPathAsync(path, canonicalWindowId, cancellationToken);
        return _currentDocument;
    }

    /// <inheritdoc />
    public Task<FrontedBehaviorDocument> LoadBuiltInDocumentAsync(
        string canonicalWindowId,
        CancellationToken cancellationToken = default)
    {
        return LoadDocumentFromPathAsync(GetBuiltInBehaviorPath(canonicalWindowId), canonicalWindowId, cancellationToken);
    }

    private async Task<FrontedBehaviorDocument> LoadDocumentFromPathAsync(
        string? path,
        string canonicalWindowId,
        CancellationToken cancellationToken)
    {
        if (path is null || !File.Exists(path))
        {
            return CreateEmptyDocument(canonicalWindowId);
        }

        try
        {
            if (new FileInfo(path).Length > FrontedLayoutLimits.MaxLayoutJsonBytes)
            {
                throw new InvalidDataException("BehaviorJsonTooLarge");
            }

            var json = await File.ReadAllTextAsync(path, cancellationToken);
            var document = JsonSerializer.Deserialize<FrontedBehaviorDocument>(json, _jsonSerializerOptions)
                           ?? CreateEmptyDocument(canonicalWindowId);
            NormalizeDocument(document, canonicalWindowId);
            return document;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Failed to load fronted behavior document. Window: {WindowType}, Canvas: {CanvasName}, Path: {Path}",
                canonicalWindowId,
                FrontedLayoutConstants.BaseCanvasName,
                path);
            return CreateEmptyDocument(canonicalWindowId);
        }
    }

    /// <inheritdoc />
    public async Task SaveDocumentAsync(
        FrontedBehaviorDocument document,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(document.WindowType))
        {
            throw new ArgumentException("Behavior document WindowType is required.", nameof(document));
        }

        NormalizeDocument(document, document.WindowType);
        RemoveEmptySets(document);
        var path = await ResolveSavePathAsync(document.WindowType, cancellationToken);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(document, _jsonSerializerOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken);
        _currentDocument = document;
    }

    /// <inheritdoc />
    public void RemoveBehaviors(Guid behaviorGuid)
    {
        _currentDocument?.RemoveSet(behaviorGuid);
    }

    private async Task<string?> ResolveLoadPathAsync(
        string canonicalWindowId,
        CancellationToken cancellationToken)
    {
        var active = await _packageManager.GetActivePackageStateAsync(cancellationToken);
        if (string.Equals(active.PackageId, FrontedLayoutPackageManager.BuiltInPackageId, StringComparison.OrdinalIgnoreCase))
        {
            return GetBuiltInBehaviorPath(canonicalWindowId);
        }

        return GetPackageBehaviorPath(active.PackageId, canonicalWindowId);
    }

    private async Task<string> ResolveSavePathAsync(
        string canonicalWindowId,
        CancellationToken cancellationToken)
    {
        var package = await _packageManager.EnsureWritableActivePackageAsync(cancellationToken);
        return GetPackageBehaviorPath(package.PackageId, canonicalWindowId);
    }

    private string GetPackageBehaviorPath(string packageId, string canonicalWindowId)
    {
        var layoutsRoot = _packageManager.GetPackageLayoutsRootFolder(packageId);
        var packageRoot = Path.GetDirectoryName(Path.GetFullPath(layoutsRoot))
                          ?? throw new InvalidOperationException("Package layouts root has no parent.");
        return Path.Combine(packageRoot, GetBehaviorRelativePath(canonicalWindowId));
    }

    private string GetBuiltInBehaviorPath(string canonicalWindowId)
    {
        var layoutsRoot = _packageManager.GetPackageLayoutsRootFolder(FrontedLayoutPackageManager.BuiltInPackageId);
        var resourcesRoot = Path.GetDirectoryName(Path.GetFullPath(layoutsRoot))
                            ?? throw new InvalidOperationException("Built-in layouts root has no parent.");
        var layoutRelativePath = FrontedV3LayoutWindowPathHelper.GetLayoutRelativePath(canonicalWindowId);
        var folder = Path.GetDirectoryName(layoutRelativePath);
        var fileName = $"{Path.GetFileNameWithoutExtension(layoutRelativePath)}.behaviors.json";
        return string.IsNullOrWhiteSpace(folder)
            ? Path.Combine(resourcesRoot, "FrontedBehaviors", fileName)
            : Path.Combine(resourcesRoot, "FrontedBehaviors", folder, fileName);
    }

    private static string GetBehaviorRelativePath(string canonicalWindowId)
    {
        var layoutRelativePath = FrontedV3LayoutWindowPathHelper.GetLayoutRelativePath(canonicalWindowId);
        var folder = Path.GetDirectoryName(layoutRelativePath);
        var fileName = $"{Path.GetFileNameWithoutExtension(layoutRelativePath)}.behaviors.json";
        if (string.IsNullOrWhiteSpace(folder))
        {
            return Path.Combine("FrontedBehaviors", fileName);
        }

        return Path.Combine("FrontedBehaviors", folder, fileName);
    }

    private static FrontedBehaviorDocument CreateEmptyDocument(string canonicalWindowId)
    {
        return new FrontedBehaviorDocument
        {
            Version = 1,
            WindowType = canonicalWindowId,
            CanvasName = FrontedLayoutConstants.BaseCanvasName
        };
    }

    private static void NormalizeDocument(FrontedBehaviorDocument document, string canonicalWindowId)
    {
        document.Version = 1;
        document.WindowType = string.IsNullOrWhiteSpace(document.WindowType) ? canonicalWindowId : document.WindowType;
        document.CanvasName = FrontedLayoutConstants.BaseCanvasName;
        document.ControlBehaviorSets ??= [];

        foreach (var set in document.ControlBehaviorSets)
        {
            set.Behaviors ??= [];
            foreach (var behavior in set.Behaviors)
            {
                behavior.Graph ??= new FrontedNodeGraph();
                behavior.StartGraph ??= new FrontedNodeGraph();
                behavior.LoopGraph ??= new FrontedNodeGraph();
                behavior.StopGraph ??= new FrontedNodeGraph();
                behavior.ExitGraph ??= new FrontedNodeGraph();
                behavior.EnterGraph ??= new FrontedNodeGraph();
                behavior.LoopPolicy ??= new FrontedLoopPolicy();
            }
        }
    }

    private static void RemoveEmptySets(FrontedBehaviorDocument document)
    {
        document.ControlBehaviorSets.RemoveAll(set => set.BehaviorGuid == Guid.Empty || set.Behaviors.Count == 0);
    }
}
