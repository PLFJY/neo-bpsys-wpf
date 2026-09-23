using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.Archives;
using neo_bpsys_wpf.Core.Models.SmartBpModule;
using neo_bpsys_wpf.ProductTour;

namespace neo_bpsys_wpf.Services.SmartBpModule;

/// <summary>
/// 负责加载、校验、安装和持久化 SmartBP 模块。
/// </summary>
public sealed partial class SmartBpModuleManager
{
    private const string GitHubReleaseDownloadBaseUrl = "https://github.com/PLFJY/neo-bpsys-wpf/releases/download";
    private const string ModuleManifestAssetName = "SmartBpModuleManifest.json";
    private const string ModuleManifestFallbackBaseUrl = "https://smartbp-module-manifest.plfjy.top/";
    private const string ModuleRegistrySubKey = @"Software\neo-bpsys-wpf\SmartBpModule";
    private const string ModuleRegistryRootValueName = "ModuleRoot";
    private const string PendingArchiveImportDirectoryName = "SmartBpModulePending";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static readonly string[] KnownOcrModelKeys =
    [
        "zh-cn-v5-mobile",
        "en-v4-mobile",
        "ja-v4-mobile",
        "zh-cn-v4",
        "zh-cn-v3-slim"
    ];

    private static readonly HashSet<string> ManagedAssetRootNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "OCRModels",
        "AI"
    };

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SmartBpModuleManager> _logger;
    private readonly ISettingsHostService _settingsHostService;
    private readonly IArchiveService _archiveService;
    private readonly IFileDownloadService _fileDownloadService;
    private static readonly object NativeSearchPathSync = new();
    private static readonly HashSet<string> RegisteredNativeSearchDirectories = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, IntPtr> PreloadedNativeLibraries = new(StringComparer.OrdinalIgnoreCase);
    private ISmartBpModuleEntryPoint? _entryPoint;
    private IReadOnlyList<SmartBpFeatureCommand> _featureCommands = [];
    private ISmartBpPostGameRecognitionProgressSource? _postGameRecognitionProgressSource;
    private bool _isModuleVersionOutdated;
    private IFileDownloadOperation? _currentModuleDownload;

    /// <summary>
    /// 初始化 <see cref="SmartBpModuleManager"/> 类的新实例。
    /// </summary>
    /// <param name="serviceProvider">宿主服务提供程序。</param>
    /// <param name="logger">日志记录器。</param>
    /// <param name="settingsHostService">设置宿主服务。</param>
    /// <param name="archiveService">压缩包解压服务。</param>
    /// <param name="fileDownloadService">统一文件下载服务。</param>
    public SmartBpModuleManager(
        IServiceProvider serviceProvider,
        ILogger<SmartBpModuleManager> logger,
        ISettingsHostService settingsHostService,
        IArchiveService archiveService,
        IFileDownloadService fileDownloadService)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _settingsHostService = settingsHostService;
        _archiveService = archiveService;
        _fileDownloadService = fileDownloadService;
        ModuleRoot = GetDefaultModuleRoot();
    }

    /// <summary>
    /// 模块加载状态变化时触发。
    /// </summary>
    public event EventHandler? ModuleStateChanged;

    /// <summary>
    /// 模块包下载状态变化时触发。
    /// </summary>
    public event EventHandler? DownloadStateChanged;

    /// <summary>
    /// 获取模块包是否正在下载或等待恢复。
    /// </summary>
    public bool IsDownloading => _currentModuleDownload?.State
        is FileDownloadState.Downloading or FileDownloadState.Paused;

    /// <summary>
    /// 获取模块包下载是否已暂停。
    /// </summary>
    public bool IsDownloadPaused => _currentModuleDownload?.State == FileDownloadState.Paused;

    /// <summary>
    /// 获取模块包下载的最新进度快照。
    /// </summary>
    public FileDownloadProgress ModuleDownloadProgress =>
        _currentModuleDownload?.Progress ?? FileDownloadProgress.Empty;

    /// <summary>
    /// 暂停当前模块包下载。
    /// </summary>
    public void PauseDownload() => _currentModuleDownload?.Pause();

    /// <summary>
    /// 恢复当前模块包下载。
    /// </summary>
    public void ResumeDownload() => _currentModuleDownload?.Resume();

    /// <summary>
    /// 取消当前模块包下载并保留部分文件以供下次续传。
    /// </summary>
    public void CancelDownload() => _currentModuleDownload?.Cancel();

    /// <summary>
    /// 已加载模块的本地版本低于远程发布标签要求的版本时触发。
    /// 该检查在模块加载成功后异步进行，不会阻塞加载流程。
    /// </summary>
    public event EventHandler<ModuleVersionOutdatedEventArgs>? ModuleVersionOutdated;

    /// <summary>
    /// 获取已加载的模块内容对象。
    /// </summary>
    public object? ModuleContent { get; private set; }

    /// <summary>
    /// 获取当前模块根目录。
    /// </summary>
    public string ModuleRoot { get; private set; }

    /// <summary>
    /// 获取模块是否已加载且版本未过时。
    /// 版本过旧时返回 <see langword="false"/>，使全局表现为模块未加载。
    /// </summary>
    public bool IsModuleLoaded => _entryPoint != null && ModuleContent != null && !_isModuleVersionOutdated;

    /// <summary>
    /// 获取模块程序集是否已物理加载（不考虑版本是否过时）。
    /// 仅用于内部安装/替换流程中判断是否需要暂存到下次重启。
    /// </summary>
    private bool IsModulePhysicallyLoaded => _entryPoint != null && ModuleContent != null;

    /// <summary>
    /// 获取已加载模块的版本是否低于远程发布标签要求的版本。
    /// </summary>
    public bool IsModuleVersionOutdated => _isModuleVersionOutdated;

    /// <summary>
    /// 获取远程发布标签要求的最小兼容版本号；版本未过旧时为 <see langword="null"/>。
    /// </summary>
    public string? RequiredModuleVersion { get; private set; }

    /// <summary>
    /// 调试用：手动设置模块版本过旧状态，用于验证更新遮罩和全局降级表现。
    /// 仅在调试构建下有效。
    /// </summary>
    /// <param name="outdated">是否标记为版本过旧。</param>
    /// <param name="requiredVersion">模拟的要求版本号。</param>
    public void SetVersionOutdatedForDebug(bool outdated, string requiredVersion = "999.0.0")
    {
#if DEBUG
        _isModuleVersionOutdated = outdated;
        RequiredModuleVersion = outdated ? requiredVersion : null;
        ModuleStateChanged?.Invoke(this, EventArgs.Empty);
#endif
    }

    /// <summary>
    /// 获取最近一次模块加载或校验失败消息。
    /// </summary>
    public string LastFailureMessage { get; private set; } = string.Empty;

    /// <summary>
    /// 获取最近一次模块压缩包导入是否已暂存且需要重启应用才能完成。
    /// </summary>
    public bool IsRestartRequiredForPendingModuleImport { get; private set; }

    /// <summary>
    /// 获取状态文件路径。
    /// </summary>
    public static string StateFilePath => Path.Combine(AppConstants.AppDataPath, "SmartBpModuleState.json");

    /// <summary>
    /// 获取待完成模块目录迁移标记文件路径。
    /// </summary>
    public static string MovePendingFilePath => Path.Combine(AppConstants.AppDataPath, "SmartBpModuleMovePending.json");

    /// <summary>
    /// 获取默认模块根目录路径。
    /// </summary>
    public static string GetDefaultModuleRoot() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "neo-bpsys-wpf", "Components", "SmartBpModule");

    /// <summary>
    /// 读取已持久化的模块状态。
    /// </summary>
    /// <returns>已持久化的状态；不存在或读取失败时返回 <see langword="null"/>。</returns>
    public SmartBpModuleState? ReadState()
    {
        try
        {
            if (!File.Exists(StateFilePath))
            {
                _logger.LogDebug("SmartBP module state file does not exist: {StateFilePath}", StateFilePath);
                return null;
            }

            _logger.LogDebug("Reading SmartBP module state: {StateFilePath}", StateFilePath);
            return JsonSerializer.Deserialize<SmartBpModuleState>(File.ReadAllText(StateFilePath), JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read SmartBP module state.");
            return null;
        }
    }

    /// <summary>
    /// 获取用于界面显示和启动加载的首选模块根目录。
    /// </summary>
    /// <returns>首选模块根目录。</returns>
    public string GetPreferredModuleRoot()
    {
        var pending = ReadMovePendingState();
        if (!string.IsNullOrWhiteSpace(pending?.TargetRoot))
        {
            return pending.TargetRoot;
        }

        return ReadState()?.ModuleRoot ?? GetDefaultModuleRoot();
    }

    /// <summary>
    /// 判断是否存在已保存的模块根目录或待完成迁移目标。
    /// </summary>
    /// <returns>存在已持久化模块根目录偏好时返回 <see langword="true"/>。</returns>
    public bool HasPersistedModuleRoot()
    {
        var pending = ReadMovePendingState();
        if (!string.IsNullOrWhiteSpace(pending?.TargetRoot))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(ReadState()?.ModuleRoot);
    }

    /// <summary>
    /// 持久化首选 SmartBP 模块根目录，但不把模块标记为已成功加载。
    /// </summary>
    /// <param name="moduleRoot">首选模块根目录路径。</param>
    public void PersistModuleRootPreference(string moduleRoot)
    {
        if (string.IsNullOrWhiteSpace(moduleRoot))
            return;

        try
        {
            var normalizedRoot = Path.GetFullPath(moduleRoot);
            var state = ReadState() ?? new SmartBpModuleState();
            var isCurrentLoadedModule = IsModuleLoaded &&
                                        string.Equals(
                                            Path.GetFullPath(ModuleRoot),
                                            normalizedRoot,
                                            StringComparison.OrdinalIgnoreCase);

            state.ModuleRoot = normalizedRoot;
            if (!isCurrentLoadedModule)
            {
                ModuleRoot = normalizedRoot;
                state.ModuleVersion = null;
                state.RuntimeAbiVersion = null;
                state.Rid = null;
                state.InstallKind = "PreferredDirectory";
                state.LastLoadedSuccessfully = false;
                state.LastLoadedAt = null;
            }

            WriteState(state);
            _logger.LogInformation("Persisted SmartBP module root preference: {ModuleRoot}", normalizedRoot);
            ModuleStateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist SmartBP module root preference: {ModuleRoot}", moduleRoot);
        }
    }

    /// <summary>
    /// 当已有模块可用时，通过暂存复制方式更新首选 SmartBP 模块目录。
    /// </summary>
    /// <param name="targetRoot">目标模块根目录。</param>
    /// <returns>目标目录准备完成且已持久化时返回 <see langword="true"/>。</returns>
    public async Task<bool> MigrateModuleRootPreferenceAsync(string targetRoot)
    {
        if (string.IsNullOrWhiteSpace(targetRoot))
        {
            LastFailureMessage = "Target module path is empty.";
            return false;
        }

        var normalizedTarget = Path.GetFullPath(targetRoot);
        if (IsUnsafeInstallPath(normalizedTarget))
        {
            LastFailureMessage = "Target module path is unsafe or not writable.";
            _logger.LogWarning("Rejected unsafe SmartBP module migration target: {TargetRoot}", normalizedTarget);
            return false;
        }

        var state = ReadState();
        var sourceRoot = state?.ModuleRoot;
        if (string.IsNullOrWhiteSpace(sourceRoot) || !Directory.Exists(sourceRoot))
        {
            _logger.LogInformation(
                "No existing SmartBP module directory found. Persisting target path without copy. TargetRoot={TargetRoot}",
                normalizedTarget);
            PersistModuleRootPreference(normalizedTarget);
            return true;
        }

        var normalizedSource = Path.GetFullPath(sourceRoot);
        if (string.Equals(normalizedSource, normalizedTarget, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("SmartBP module migration target is same as source: {ModuleRoot}", normalizedTarget);
            PersistModuleRootPreference(normalizedTarget);
            return true;
        }

        if (IsSameOrChildPath(normalizedTarget, normalizedSource) ||
            IsSameOrChildPath(normalizedSource, normalizedTarget))
        {
            LastFailureMessage = "Target module path must not be the source directory or its parent/child.";
            _logger.LogWarning(
                "Rejected nested SmartBP module migration path. SourceRoot={SourceRoot}, TargetRoot={TargetRoot}",
                normalizedSource,
                normalizedTarget);
            return false;
        }

        if (!ValidateModuleDirectory(
                normalizedSource,
                allowDevelopmentDirectory: false,
                out var sourceManifest,
                out var sourceValidationError))
        {
            LastFailureMessage = sourceValidationError;
            _logger.LogWarning(
                "SmartBP module migration source failed validation. SourceRoot={SourceRoot}, Error={Error}",
                normalizedSource,
                sourceValidationError);
            return false;
        }

        try
        {
            await CopyModuleRootForMigrationAsync(normalizedSource, normalizedTarget);
            WriteMovePendingState(new SmartBpModuleMovePendingState
            {
                SourceRoot = normalizedSource,
                TargetRoot = normalizedTarget,
                CreatedAt = DateTimeOffset.UtcNow
            });

            WriteState(new SmartBpModuleState
            {
                ModuleRoot = normalizedTarget,
                ModuleVersion = sourceManifest?.ModuleVersion ?? state?.ModuleVersion,
                RuntimeAbiVersion = sourceManifest?.RuntimeAbiVersion ?? state?.RuntimeAbiVersion,
                Rid = sourceManifest?.Rid ?? state?.Rid,
                InstallKind = "PathMigrationPending",
                LastLoadedSuccessfully = false,
                LastLoadedAt = null,
                LegacyOcrModelMigration = state?.LegacyOcrModelMigration ?? new SmartBpLegacyOcrModelMigrationState()
            });

            ModuleRoot = normalizedTarget;
            LastFailureMessage = string.Empty;
            _logger.LogInformation(
                "SmartBP module path migration prepared. SourceRoot={SourceRoot}, TargetRoot={TargetRoot}, Marker={Marker}",
                normalizedSource,
                normalizedTarget,
                MovePendingFilePath);
            ModuleStateChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception ex)
        {
            LastFailureMessage = FormatExceptionForUser(ex);
            _logger.LogError(
                ex,
                "Failed to prepare SmartBP module path migration. SourceRoot={SourceRoot}, TargetRoot={TargetRoot}",
                normalizedSource,
                normalizedTarget);
            return false;
        }
    }

}
