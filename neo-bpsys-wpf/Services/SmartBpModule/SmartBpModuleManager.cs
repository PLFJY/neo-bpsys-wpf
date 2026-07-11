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
using neo_bpsys_wpf.Core.Models.SmartBpModule;
using neo_bpsys_wpf.ProductTour;

namespace neo_bpsys_wpf.Services.SmartBpModule;

/// <summary>
/// 负责加载、校验、安装和持久化 SmartBP 模块。
/// </summary>
public sealed class SmartBpModuleManager
{
    private const string ReleaseApiUrl = "https://gh-releases.plfjy.top/?repo=PLFJY/neo-bpsys-wpf&ua=neo-bpsys-wpf";
    private const string ModuleManifestDownloadMirror = "https://gh.plfjy.top/";
    private const string ModuleManifestAssetName = "SmartBpModuleManifest.json";
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
    private static readonly object NativeSearchPathSync = new();
    private static readonly HashSet<string> RegisteredNativeSearchDirectories = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, IntPtr> PreloadedNativeLibraries = new(StringComparer.OrdinalIgnoreCase);
    private ISmartBpModuleEntryPoint? _entryPoint;
    private IReadOnlyList<SmartBpFeatureCommand> _featureCommands = [];

    /// <summary>
    /// 初始化 <see cref="SmartBpModuleManager"/> 类的新实例。
    /// </summary>
    /// <param name="serviceProvider">宿主服务Provider。</param>
    /// <param name="logger">日志记录器。</param>
    /// <param name="settingsHostService">设置宿主服务。</param>
    /// <param name="archiveService">压缩包解压服务。</param>
    public SmartBpModuleManager(
        IServiceProvider serviceProvider,
        ILogger<SmartBpModuleManager> logger,
        ISettingsHostService settingsHostService,
        IArchiveService archiveService)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _settingsHostService = settingsHostService;
        _archiveService = archiveService;
        ModuleRoot = GetDefaultModuleRoot();
    }

    /// <summary>
    /// 模块加载状态变化时触发。
    /// </summary>
    public event EventHandler? ModuleStateChanged;

    /// <summary>
    /// 获取已加载的模块内容对象。
    /// </summary>
    public object? ModuleContent { get; private set; }

    /// <summary>
    /// 获取当前模块根目录。
    /// </summary>
    public string ModuleRoot { get; private set; }

    /// <summary>
    /// 获取模块是否已加载。
    /// </summary>
    public bool IsModuleLoaded => _entryPoint != null && ModuleContent != null;

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

    /// <summary>
    /// 在可用时加载已持久化的模块。
    /// </summary>
    /// <returns>成功加载时返回 <see langword="true"/>。</returns>
    public async Task<bool> TryLoadPersistedModuleAsync()
    {
        var state = ReadState();
        var pending = ReadMovePendingState();
        if (!string.IsNullOrWhiteSpace(pending?.PreparedRoot))
        {
            TryCompletePendingArchiveImport(pending);
            state = ReadState();
            pending = ReadMovePendingState();
        }

        var moduleRoot = !string.IsNullOrWhiteSpace(pending?.TargetRoot)
            ? pending.TargetRoot
            : state?.ModuleRoot;
        var installKind = !string.IsNullOrWhiteSpace(pending?.TargetRoot)
            ? pending.InstallKind ?? "PathMigrationPending"
            : state?.InstallKind ?? "LocalDirectory";

        if (string.IsNullOrWhiteSpace(moduleRoot))
        {
            _logger.LogDebug("No persisted SmartBP module root to load.");
            return false;
        }

        _logger.LogInformation(
            "Loading persisted SmartBP module. ModuleRoot={ModuleRoot}, InstallKind={InstallKind}",
            moduleRoot,
            installKind);
        return await LoadModuleFromDirectoryAsync(moduleRoot, installKind);
    }

    /// <summary>
    /// 从目录加载模块。
    /// </summary>
    /// <param name="moduleRoot">模块根目录。</param>
    /// <param name="installKind">Install kind.</param>
    /// <returns>成功加载时返回 <see langword="true"/>。</returns>
    public async Task<bool> LoadModuleFromDirectoryAsync(string moduleRoot, string installKind = "LocalDirectory")
    {
        _logger.LogInformation(
            "Loading SmartBP module from directory. ModuleRoot={ModuleRoot}, InstallKind={InstallKind}",
            moduleRoot,
            installKind);

        if (!ValidateModuleDirectory(moduleRoot, allowDevelopmentDirectory: IsDebugBuild(), out var manifest, out var error))
        {
            LastFailureMessage = error;
            _logger.LogWarning("SmartBP module validation failed: {Error}", error);
            return false;
        }

        if (!IsDebugBuild() && !IsPreviewBuild() && manifest != null)
        {
            var requiredManifest = await TryFetchRequiredModuleManifestAsync();
            if (requiredManifest != null &&
                !IsModuleVersionAllowed(manifest.ModuleVersion, requiredManifest.ModuleVersion))
            {
                _logger.LogWarning(
                    "SmartBP module version is too old. Local={LocalVersion}, Required={RequiredVersion}",
                    manifest.ModuleVersion,
                    requiredManifest.ModuleVersion);
                return false;
            }
        }

        var entryAssembly = Path.Combine(moduleRoot, SmartBpModuleConstants.EntryAssemblyName);
        try
        {
            var loadContext = new AssemblyLoadContext($"SmartBpModule-{Guid.NewGuid():N}", isCollectible: false);
            RegisterModuleNativeSearchDirectories(moduleRoot, _logger);
            loadContext.Resolving += (_, name) =>
            {
                _logger.LogDebug("Resolving SmartBP module assembly: {AssemblyName}", name.FullName);
                var sharedAssembly = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(assembly =>
                    AssemblyName.ReferenceMatchesDefinition(assembly.GetName(), name));
                if (sharedAssembly != null)
                {
                    _logger.LogDebug("Resolved SmartBP module assembly from host context: {AssemblyName}", name.FullName);
                    return sharedAssembly;
                }

                try
                {
                    var hostAssembly = AssemblyLoadContext.Default.LoadFromAssemblyName(name);
                    _logger.LogDebug("Loaded SmartBP module assembly from default context: {AssemblyName}", name.FullName);
                    return hostAssembly;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "SmartBP module assembly was not available from default context: {AssemblyName}", name.FullName);
                    // 对 SmartBP 自有依赖继续使用模块内探测。
                }

                var candidate = string.IsNullOrWhiteSpace(name.CultureName)
                    ? Path.Combine(moduleRoot, $"{name.Name}.dll")
                    : Path.Combine(moduleRoot, name.CultureName, $"{name.Name}.dll");
                if (!File.Exists(candidate))
                {
                    _logger.LogWarning("SmartBP module dependency was not found. AssemblyName={AssemblyName}, Candidate={Candidate}", name.FullName, candidate);
                    return null;
                }

                _logger.LogDebug("Loading SmartBP module dependency from module root: {Candidate}", candidate);
                return loadContext.LoadFromAssemblyPath(candidate);
            };
            loadContext.ResolvingUnmanagedDll += (_, libraryName) =>
            {
                var candidate = FindModuleUnmanagedLibraryPath(moduleRoot, libraryName);
                if (candidate == null)
                {
                    _logger.LogWarning(
                        "SmartBP module native dependency was not found. LibraryName={LibraryName}, ModuleRoot={ModuleRoot}",
                        libraryName,
                        moduleRoot);
                    return IntPtr.Zero;
                }

                try
                {
                    _logger.LogDebug(
                        "Loading SmartBP module native dependency. LibraryName={LibraryName}, Candidate={Candidate}",
                        libraryName,
                        candidate);
                    return NativeLibrary.Load(candidate);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to load SmartBP module native dependency. LibraryName={LibraryName}, Candidate={Candidate}",
                        libraryName,
                        candidate);
                    return IntPtr.Zero;
                }
            };
            _logger.LogInformation("Loading SmartBP module entry assembly: {EntryAssembly}", entryAssembly);
            var assembly = loadContext.LoadFromAssemblyPath(entryAssembly);
            var entryType = assembly.GetTypes()
                .FirstOrDefault(t => typeof(ISmartBpModuleEntryPoint).IsAssignableFrom(t) && !t.IsAbstract);
            if (entryType == null)
            {
                LastFailureMessage = "Module entry type was not found.";
                _logger.LogWarning("SmartBP module entry type was not found in assembly: {EntryAssembly}", entryAssembly);
                return false;
            }

            _logger.LogInformation("Creating SmartBP module entry point. EntryType={EntryType}", entryType.FullName);
            _entryPoint = (ISmartBpModuleEntryPoint)Activator.CreateInstance(entryType)!;
            ModuleRoot = moduleRoot;

            if (_entryPoint is ITutorialRegistrationContributor contributor)
            {
                _logger.LogInformation(
                    "Registering SmartBP module tutorial contributor. RegistrationId={RegistrationId}",
                    contributor.RegistrationId);
                var registrationService = _serviceProvider.GetService<ITutorialRegistrationService>();
                if (registrationService == null)
                {
                    throw new InvalidOperationException(
                        "ITutorialRegistrationService is not available. Cannot register SmartBP module tutorials.");
                }

                registrationService.RegisterContributor(contributor);
            }

            _logger.LogInformation("Creating SmartBP module content.");
            ModuleContent = _entryPoint.CreateSmartBpContent(_serviceProvider);
            _featureCommands = _entryPoint.GetFeatureCommands();
            _logger.LogInformation("SmartBP module content created. FeatureCommandCount={FeatureCommandCount}", _featureCommands.Count);
            LastFailureMessage = string.Empty;
            await MigrateLegacyOcrModelsOnceAsync(moduleRoot);
            WriteState(new SmartBpModuleState
            {
                ModuleRoot = moduleRoot,
                ModuleVersion = manifest?.ModuleVersion,
                RuntimeAbiVersion = manifest?.RuntimeAbiVersion,
                Rid = manifest?.Rid,
                InstallKind = installKind,
                LastLoadedSuccessfully = true,
                LastLoadedAt = DateTimeOffset.UtcNow,
                LegacyOcrModelMigration = ReadState()?.LegacyOcrModelMigration ?? new SmartBpLegacyOcrModelMigrationState { Completed = true, Reason = "Completed" }
            });
            CompletePendingModuleRootMigration(moduleRoot);
            ModuleStateChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception ex)
        {
            LastFailureMessage = FormatExceptionForUser(ex);
            _logger.LogError(ex, "Failed to load SmartBP module from {ModuleRoot}", moduleRoot);
            return false;
        }
    }

    /// <summary>
    /// 下载当前应用标签对应的 SmartBP 模块包，并通过暂存目录完成安装。
    /// </summary>
    /// <param name="targetRoot">Final target root.</param>
    /// <param name="progress">Optional progress reporter from 0 to 100.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>成功下载并安装，或已暂存并等待重启时返回 <see langword="true"/>。</returns>
    public async Task<bool> DownloadAndInstallCurrentModuleAsync(
        string targetRoot,
        IProgress<double>? progress,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting SmartBP module download and install. TargetRoot={TargetRoot}", targetRoot);
        if (IsPreviewBuild())
        {
            _logger.LogInformation("SmartBP module online install is disabled in Preview builds.");
            return false;
        }

        if (IsUnsafeInstallPath(targetRoot))
        {
            _logger.LogWarning("SmartBP module target root is unsafe or not writable: {TargetRoot}", targetRoot);
            return false;
        }

        var manifest = await TryFetchRequiredModuleManifestAsync();
        if (manifest == null || string.IsNullOrWhiteSpace(manifest.Asset.Url))
        {
            _logger.LogWarning("SmartBP module manifest is unavailable or missing asset URL.");
            return false;
        }

        var url = GetMirroredDownloadUrl(manifest.Asset.Url.Replace("{tag}", AppConstants.AppVersion, StringComparison.OrdinalIgnoreCase));
        var tempRoot = Path.Combine(AppConstants.AppTempPath, "SmartBpModule", Guid.NewGuid().ToString("N"));
        var archivePath = Path.Combine(tempRoot, manifest.Asset.Name);
        Directory.CreateDirectory(tempRoot);
        try
        {
            progress?.Report(5);
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(AppConstants.AppName);
            _logger.LogInformation(
                "Downloading SmartBP module package. Url={Url}, ArchivePath={ArchivePath}",
                url,
                archivePath);
            await using (var input = await httpClient.GetStreamAsync(url, cancellationToken))
            await using (var output = File.Create(archivePath))
            {
                await input.CopyToAsync(output, cancellationToken);
            }

            progress?.Report(70);
            if (!string.IsNullOrWhiteSpace(manifest.Asset.Sha256))
            {
                var actual = ComputeSha256(archivePath);
                if (!string.Equals(actual, manifest.Asset.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("SmartBP module hash mismatch. Expected={Expected}, Actual={Actual}", manifest.Asset.Sha256, actual);
                    return false;
                }
            }

            progress?.Report(80);
            var installed = await ImportArchiveAsync(archivePath, targetRoot, "LiteDownload");
            _logger.LogInformation("SmartBP module download install completed. Installed={Installed}", installed);
            progress?.Report(installed ? 100 : 80);
            return installed;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download and install SmartBP module.");
            return false;
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    /// <summary>
    /// 通过暂存目录导入模块压缩包。
    /// </summary>
    /// <param name="archivePath">压缩包路径。</param>
    /// <param name="targetRoot">Final target root.</param>
    /// <returns>成功导入并加载，或已暂存并等待重启时返回 <see langword="true"/>。</returns>
    public async Task<bool> ImportArchiveAsync(string archivePath, string targetRoot)
    {
        return await ImportArchiveAsync(archivePath, targetRoot, "PreviewArchiveImport");
    }

    /// <summary>
    /// 通过暂存目录导入模块压缩包。
    /// </summary>
    /// <param name="archivePath">压缩包路径。</param>
    /// <param name="targetRoot">Final target root.</param>
    /// <param name="installKind">Install kind persisted in module state.</param>
    /// <returns>成功导入并加载，或已暂存并等待重启时返回 <see langword="true"/>。</returns>
    public async Task<bool> ImportArchiveAsync(string archivePath, string targetRoot, string installKind)
    {
        IsRestartRequiredForPendingModuleImport = false;
        if (string.IsNullOrWhiteSpace(targetRoot))
        {
            LastFailureMessage = "Target module path is empty.";
            return false;
        }

        var normalizedTargetRoot = Path.GetFullPath(targetRoot);
        _logger.LogInformation(
            "Importing SmartBP module archive. ArchivePath={ArchivePath}, TargetRoot={TargetRoot}, InstallKind={InstallKind}",
            archivePath,
            normalizedTargetRoot,
            installKind);
        if (IsUnsafeInstallPath(normalizedTargetRoot))
        {
            _logger.LogWarning("SmartBP module import target is unsafe or not writable: {TargetRoot}", normalizedTargetRoot);
            return false;
        }

        var staging = Path.Combine(AppConstants.AppTempPath, "SmartBpModule", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            await _archiveService.ExtractToDirectoryAsync(archivePath, staging);
            var candidateRoot = File.Exists(Path.Combine(staging, "component.json"))
                ? staging
                : Directory.EnumerateDirectories(staging).FirstOrDefault() ?? staging;
            if (!ValidateModuleDirectory(candidateRoot, allowDevelopmentDirectory: false, out var manifest, out var validationError))
            {
                LastFailureMessage = validationError;
                _logger.LogWarning("Imported SmartBP module archive failed validation: {ValidationError}", validationError);
                return false;
            }

            if (IsModuleLoaded &&
                string.Equals(Path.GetFullPath(ModuleRoot), normalizedTargetRoot, StringComparison.OrdinalIgnoreCase))
            {
                PrepareArchiveImportForRestart(candidateRoot, normalizedTargetRoot, installKind, manifest);
                return true;
            }

            try
            {
                ReplaceModuleRootPreservingManagedAssets(candidateRoot, normalizedTargetRoot);
                return await LoadModuleFromDirectoryAsync(normalizedTargetRoot, installKind);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogInformation(
                    ex,
                    "SmartBP module target could not be replaced immediately. Staging import for next restart. TargetRoot={TargetRoot}",
                    normalizedTargetRoot);
                PrepareArchiveImportForRestart(candidateRoot, normalizedTargetRoot, installKind, manifest);
                return true;
            }
        }
        finally
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
        }
    }

    /// <summary>
    /// 执行功能命令。
    /// </summary>
    /// <param name="commandId">命令标识。</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Asynchronous task.</returns>
    public Task ExecuteFeatureCommandAsync(string commandId, CancellationToken cancellationToken)
    {
        var command = _featureCommands.FirstOrDefault(c => c.CommandId == commandId);
        return command?.ExecuteAsync(cancellationToken) ?? Task.CompletedTask;
    }

    /// <summary>
    /// 校验模块目录。
    /// </summary>
    /// <param name="moduleRoot">模块根目录。</param>
    /// <param name="allowDevelopmentDirectory">是否允许调试开发目录。</param>
    /// <param name="manifest">Component manifest.</param>
    /// <param name="error">校验错误。</param>
    /// <returns>校验结果。</returns>
    public bool ValidateModuleDirectory(
        string moduleRoot,
        bool allowDevelopmentDirectory,
        out SmartBpModuleManifest? manifest,
        out string error)
    {
        manifest = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(moduleRoot) || !Directory.Exists(moduleRoot))
        {
            error = "Module directory does not exist.";
            _logger.LogDebug("SmartBP module directory validation failed: {Error} ModuleRoot={ModuleRoot}", error, moduleRoot);
            return false;
        }

        var entryAssembly = Path.Combine(moduleRoot, SmartBpModuleConstants.EntryAssemblyName);
        if (!File.Exists(entryAssembly))
        {
            error = "Module entry assembly is missing.";
            _logger.LogDebug("SmartBP module directory validation failed: {Error} EntryAssembly={EntryAssembly}", error, entryAssembly);
            return false;
        }

        var componentPath = Path.Combine(moduleRoot, "component.json");
        if (!File.Exists(componentPath))
        {
            _logger.LogDebug(
                "SmartBP module component manifest is missing. AllowDevelopmentDirectory={AllowDevelopmentDirectory}, ModuleRoot={ModuleRoot}",
                allowDevelopmentDirectory,
                moduleRoot);
            return allowDevelopmentDirectory;
        }

        manifest = JsonSerializer.Deserialize<SmartBpModuleManifest>(File.ReadAllText(componentPath), JsonOptions);
        if (manifest == null || manifest.ComponentId != SmartBpModuleConstants.ComponentId)
        {
            error = "ComponentId mismatch.";
            _logger.LogDebug("SmartBP module directory validation failed: {Error} ComponentPath={ComponentPath}", error, componentPath);
            return false;
        }

        if (!string.Equals(manifest.Rid, SmartBpModuleConstants.Rid, StringComparison.OrdinalIgnoreCase))
        {
            error = "RID mismatch.";
            _logger.LogDebug("SmartBP module directory validation failed: {Error} LocalRid={LocalRid}, ExpectedRid={ExpectedRid}", error, manifest.Rid, SmartBpModuleConstants.Rid);
            return false;
        }

        if (manifest.RuntimeAbiVersion != SmartBpModuleConstants.RuntimeAbiVersion)
        {
            error = "Runtime ABI mismatch.";
            _logger.LogDebug(
                "SmartBP module directory validation failed: {Error} LocalAbi={LocalAbi}, ExpectedAbi={ExpectedAbi}",
                error,
                manifest.RuntimeAbiVersion,
                SmartBpModuleConstants.RuntimeAbiVersion);
            return false;
        }

        _logger.LogDebug(
            "SmartBP module directory validation succeeded. ModuleRoot={ModuleRoot}, ModuleVersion={ModuleVersion}, RuntimeAbiVersion={RuntimeAbiVersion}, Rid={Rid}",
            moduleRoot,
            manifest.ModuleVersion,
            manifest.RuntimeAbiVersion,
            manifest.Rid);
        return true;
    }

    /// <summary>
    /// 判断路径是否不适合作为模块安装目录。
    /// </summary>
    /// <param name="path">Candidate path.</param>
    /// <returns>路径不安全时返回 <see langword="true"/>。</returns>
    public static bool IsUnsafeInstallPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return true;

        var full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var root = Path.GetPathRoot(full)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
            return true;

        var blocked = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.SystemDirectory
        };
        return blocked.Any(b => !string.IsNullOrWhiteSpace(b) && IsSameOrChildPath(full, b)) ||
               !HasWriteAccess(full);
    }

    /// <summary>
    /// 计算文件的 SHA-256 哈希。
    /// </summary>
    /// <param name="filePath">文件路径。</param>
    /// <returns>小写 SHA-256 哈希字符串。</returns>
    public static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    /// <summary>
    /// 获取当前应用发布标签对应的 SmartBP 模块 manifest。
    /// </summary>
    /// <returns>Required module manifest, or null when offline, unavailable, or not required.</returns>
    public async Task<SmartBpModuleManifest?> TryFetchRequiredModuleManifestAsync()
    {
        if (IsDebugBuild() || IsPreviewBuild())
            return null;

        return await TryFetchCurrentTagManifestAsync();
    }

    /// <summary>
    /// 判断本地模块版本是否满足要求的模块版本。
    /// </summary>
    /// <param name="localVersion">Local module version.</param>
    /// <param name="requiredVersion">Required module version.</param>
    /// <returns>本地版本等于或新于要求版本时返回 <see langword="true"/>。</returns>
    public static bool IsModuleVersionAllowed(string localVersion, string requiredVersion)
    {
        if (Version.TryParse(localVersion.Replace('-', '.'), out var local) &&
            Version.TryParse(requiredVersion.Replace('-', '.'), out var required))
        {
            return local >= required;
        }

        return string.Compare(localVersion, requiredVersion, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// 写入 SmartBP 模块状态文件，并把模块根目录镜像到 HKCU 供卸载器清理使用。
    /// </summary>
    /// <param name="state">State to persist.</param>
    private void WriteState(SmartBpModuleState state)
    {
        Directory.CreateDirectory(AppConstants.AppDataPath);
        File.WriteAllText(StateFilePath, JsonSerializer.Serialize(state, JsonOptions));
        WriteModuleRootRegistryValue(state.ModuleRoot);
        _logger.LogDebug(
            "SmartBP module state written. StateFilePath={StateFilePath}, ModuleRoot={ModuleRoot}, InstallKind={InstallKind}, LastLoadedSuccessfully={LastLoadedSuccessfully}",
            StateFilePath,
            state.ModuleRoot,
            state.InstallKind,
            state.LastLoadedSuccessfully);
    }

    /// <summary>
    /// 将模块根目录保存到当前用户注册表配置单元，供不便解析 JSON 状态文件的工具使用。
    /// </summary>
    /// <param name="moduleRoot">要写入的模块根目录路径。</param>
    private void WriteModuleRootRegistryValue(string? moduleRoot)
    {
        if (string.IsNullOrWhiteSpace(moduleRoot))
        {
            _logger.LogDebug("Skipped SmartBP module registry update because ModuleRoot is empty.");
            return;
        }

        try
        {
            var normalizedRoot = Path.GetFullPath(moduleRoot);
            using var key = Registry.CurrentUser.CreateSubKey(ModuleRegistrySubKey);
            key?.SetValue(ModuleRegistryRootValueName, normalizedRoot, RegistryValueKind.String);
            _logger.LogDebug(
                "SmartBP module registry path written. SubKey={SubKey}, ValueName={ValueName}, ModuleRoot={ModuleRoot}",
                ModuleRegistrySubKey,
                ModuleRegistryRootValueName,
                normalizedRoot);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write SmartBP module registry path for uninstall cleanup. ModuleRoot={ModuleRoot}", moduleRoot);
        }
    }

    /// <summary>
    /// 暂存导入的模块压缩包，使下一次进程启动时可以替换当前活动模块目录。
    /// </summary>
    /// <param name="candidateRoot">Temporary extracted candidate module root.</param>
    /// <param name="targetRoot">Final module root that should be replaced on restart.</param>
    /// <param name="installKind">Install source label written to module state.</param>
    /// <param name="manifest">Validated module manifest, if one was read.</param>
    /// <exception cref="InvalidOperationException">Thrown when the staged path overlaps the target path.</exception>
    private void PrepareArchiveImportForRestart(
        string candidateRoot,
        string targetRoot,
        string installKind,
        SmartBpModuleManifest? manifest)
    {
        var normalizedCandidateRoot = Path.GetFullPath(candidateRoot);
        var normalizedTargetRoot = Path.GetFullPath(targetRoot);
        var pendingParent = Path.Combine(AppConstants.AppDataPath, PendingArchiveImportDirectoryName);
        var preparedRoot = Path.Combine(pendingParent, Guid.NewGuid().ToString("N"));
        var state = ReadState();

        if (IsSameOrChildPath(preparedRoot, normalizedTargetRoot) ||
            IsSameOrChildPath(normalizedTargetRoot, preparedRoot))
        {
            throw new InvalidOperationException("Pending SmartBP module path overlaps the target module path.");
        }

        // 已加载模块可能持有 native DLL 句柄，因此压缩包导入先暂存，
        // 并在下一次进程启动、模块加载前完成替换。
        CleanupExistingPendingArchiveImport();
        Directory.CreateDirectory(pendingParent);
        Directory.Move(normalizedCandidateRoot, preparedRoot);

        WriteMovePendingState(new SmartBpModuleMovePendingState
        {
            SourceRoot = normalizedTargetRoot,
            TargetRoot = normalizedTargetRoot,
            PreparedRoot = preparedRoot,
            InstallKind = installKind,
            CreatedAt = DateTimeOffset.UtcNow
        });

        WriteState(new SmartBpModuleState
        {
            ModuleRoot = normalizedTargetRoot,
            ModuleVersion = manifest?.ModuleVersion ?? state?.ModuleVersion,
            RuntimeAbiVersion = manifest?.RuntimeAbiVersion ?? state?.RuntimeAbiVersion,
            Rid = manifest?.Rid ?? state?.Rid,
            InstallKind = installKind,
            LastLoadedSuccessfully = false,
            LastLoadedAt = null,
            LegacyOcrModelMigration = state?.LegacyOcrModelMigration ?? new SmartBpLegacyOcrModelMigrationState()
        });

        ModuleRoot = normalizedTargetRoot;
        LastFailureMessage = string.Empty;
        IsRestartRequiredForPendingModuleImport = true;
        _logger.LogInformation(
            "SmartBP module archive import staged for next restart. PreparedRoot={PreparedRoot}, TargetRoot={TargetRoot}, InstallKind={InstallKind}",
            preparedRoot,
            normalizedTargetRoot,
            installKind);
        ModuleStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 当新的压缩包导入取代旧导入时，移除先前暂存的导入目录。
    /// </summary>
    private void CleanupExistingPendingArchiveImport()
    {
        var pending = ReadMovePendingState();
        if (string.IsNullOrWhiteSpace(pending?.PreparedRoot))
        {
            return;
        }

        try
        {
            if (Directory.Exists(pending.PreparedRoot))
            {
                Directory.Delete(pending.PreparedRoot, recursive: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean previous pending SmartBP module archive import: {PreparedRoot}", pending.PreparedRoot);
        }
    }

    /// <summary>
    /// 在当前进程尝试加载 SmartBP 前完成已暂存的压缩包导入。
    /// </summary>
    /// <param name="pending">Pending move/import marker read from disk.</param>
    /// <returns><see langword="true"/> when there is nothing left to do or replacement succeeded; otherwise <see langword="false"/>.</returns>
    private bool TryCompletePendingArchiveImport(SmartBpModuleMovePendingState pending)
    {
        if (string.IsNullOrWhiteSpace(pending.PreparedRoot) ||
            string.IsNullOrWhiteSpace(pending.TargetRoot))
        {
            return true;
        }

        try
        {
            var preparedRoot = Path.GetFullPath(pending.PreparedRoot);
            var targetRoot = Path.GetFullPath(pending.TargetRoot);
            if (!Directory.Exists(preparedRoot))
            {
                pending.LastCleanupError = "Prepared SmartBP module directory is missing.";
                WriteMovePendingState(pending);
                _logger.LogWarning(
                    "Pending SmartBP module archive import cannot continue because prepared directory is missing: {PreparedRoot}",
                    preparedRoot);
                return false;
            }

            if (IsUnsafeInstallPath(targetRoot) ||
                IsSameOrChildPath(preparedRoot, targetRoot) ||
                IsSameOrChildPath(targetRoot, preparedRoot))
            {
                pending.LastCleanupError = "Pending SmartBP module replacement path is unsafe.";
                WriteMovePendingState(pending);
                _logger.LogWarning(
                    "Pending SmartBP module archive import rejected unsafe paths. PreparedRoot={PreparedRoot}, TargetRoot={TargetRoot}",
                    preparedRoot,
                    targetRoot);
                return false;
            }

            if (!ValidateModuleDirectory(
                    preparedRoot,
                    allowDevelopmentDirectory: false,
                    out _,
                    out var validationError))
            {
                pending.LastCleanupError = validationError;
                WriteMovePendingState(pending);
                _logger.LogWarning(
                    "Pending SmartBP module archive import failed validation. PreparedRoot={PreparedRoot}, Error={Error}",
                    preparedRoot,
                    validationError);
                return false;
            }

            // 运行时托管模型可能体积较大且由用户下载；替换代码/资源时
            // 保留同一模块根目录下的 OCR 或 AI 资产。
            ReplaceModuleRootPreservingManagedAssets(preparedRoot, targetRoot);
            File.Delete(MovePendingFilePath);
            _logger.LogInformation("Completed pending SmartBP module archive import: {TargetRoot}", targetRoot);
            return true;
        }
        catch (Exception ex)
        {
            pending.LastCleanupError = FormatExceptionForUser(ex);
            WriteMovePendingState(pending);
            _logger.LogWarning(ex, "Failed to complete pending SmartBP module archive import.");
            return false;
        }
    }

    /// <summary>
    /// 通过已校验的暂存目录，把现有模块根目录复制到新的目标根目录。
    /// </summary>
    /// <param name="sourceRoot">Existing module root.</param>
    /// <param name="targetRoot">目标模块根目录。</param>
    /// <returns>复制与替换完成后结束的任务。</returns>
    /// <exception cref="InvalidOperationException">Thrown when the target or staged copy is not a valid SmartBP module.</exception>
    private async Task CopyModuleRootForMigrationAsync(string sourceRoot, string targetRoot)
    {
        var targetExists = Directory.Exists(targetRoot);
        if (targetExists && Directory.EnumerateFileSystemEntries(targetRoot).Any())
        {
            if (!ValidateModuleDirectory(
                    targetRoot,
                    allowDevelopmentDirectory: IsDebugBuild(),
                    out _,
                    out var targetValidationError))
            {
                throw new InvalidOperationException(
                    $"Target directory already contains non-SmartBP files or an invalid module: {targetValidationError}");
            }
        }

        var tempRoot = Path.Combine(AppConstants.AppTempPath, "SmartBpModuleMove", Guid.NewGuid().ToString("N"));
        var stagingRoot = Path.Combine(tempRoot, "SmartBpModule");
        Directory.CreateDirectory(tempRoot);
        try
        {
            await Task.Run(() => CopyDirectory(sourceRoot, stagingRoot));
            if (!ValidateModuleDirectory(
                    stagingRoot,
                    allowDevelopmentDirectory: IsDebugBuild(),
                    out _,
                    out var stagingValidationError))
            {
                throw new InvalidOperationException($"Copied module failed validation: {stagingValidationError}");
            }

            if (Directory.Exists(targetRoot))
            {
                _logger.LogInformation("Replacing SmartBP module migration target: {TargetRoot}", targetRoot);
                Directory.Delete(targetRoot, recursive: true);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetRoot)!);
            Directory.Move(stagingRoot, targetRoot);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 读取待完成的模块移动或压缩包导入标记。
    /// </summary>
    /// <returns>待完成移动状态；标记不存在或无法读取时返回 <see langword="null"/>。</returns>
    private SmartBpModuleMovePendingState? ReadMovePendingState()
    {
        try
        {
            if (!File.Exists(MovePendingFilePath))
            {
                return null;
            }

            return JsonSerializer.Deserialize<SmartBpModuleMovePendingState>(
                File.ReadAllText(MovePendingFilePath),
                JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read SmartBP module move marker: {Marker}", MovePendingFilePath);
            return null;
        }
    }

    /// <summary>
    /// 写入待完成的模块移动或压缩包导入标记。
    /// </summary>
    /// <param name="state">Move marker state.</param>
    private void WriteMovePendingState(SmartBpModuleMovePendingState state)
    {
        Directory.CreateDirectory(AppConstants.AppDataPath);
        File.WriteAllText(MovePendingFilePath, JsonSerializer.Serialize(state, JsonOptions));
        _logger.LogDebug(
            "SmartBP module move marker written. SourceRoot={SourceRoot}, TargetRoot={TargetRoot}, Marker={Marker}",
            state.SourceRoot,
            state.TargetRoot,
            MovePendingFilePath);
    }

    /// <summary>
    /// 切换到开发目录且不应继续此前移动时，删除待完成移动标记。
    /// </summary>
    private void DeleteMovePendingStateIfExists()
    {
        try
        {
            if (!File.Exists(MovePendingFilePath))
            {
                return;
            }

            File.Delete(MovePendingFilePath);
            _logger.LogInformation("Deleted SmartBP module move marker while switching to development directory: {Marker}", MovePendingFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete SmartBP module move marker while switching to development directory: {Marker}", MovePendingFilePath);
        }
    }

    /// <summary>
    /// 迁移后的目标根目录成功加载后清理旧模块根目录。
    /// </summary>
    /// <param name="loadedModuleRoot">刚刚完成加载的模块根目录。</param>
    private void CompletePendingModuleRootMigration(string loadedModuleRoot)
    {
        var pending = ReadMovePendingState();
        if (pending == null)
        {
            return;
        }

        var normalizedLoadedRoot = Path.GetFullPath(loadedModuleRoot);
        var normalizedTargetRoot = Path.GetFullPath(pending.TargetRoot);
        var normalizedSourceRoot = Path.GetFullPath(pending.SourceRoot);
        if (!string.Equals(normalizedLoadedRoot, normalizedTargetRoot, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug(
                "Skipping SmartBP module move cleanup because loaded root is not pending target. LoadedRoot={LoadedRoot}, TargetRoot={TargetRoot}",
                normalizedLoadedRoot,
                normalizedTargetRoot);
            return;
        }

        if (!string.IsNullOrWhiteSpace(pending.PreparedRoot))
        {
            _logger.LogDebug(
                "Skipping SmartBP module move cleanup because an archive import is still pending. PreparedRoot={PreparedRoot}, TargetRoot={TargetRoot}",
                pending.PreparedRoot,
                normalizedTargetRoot);
            return;
        }

        if (string.Equals(normalizedSourceRoot, normalizedTargetRoot, StringComparison.OrdinalIgnoreCase) ||
            IsSameOrChildPath(normalizedTargetRoot, normalizedSourceRoot) ||
            IsSameOrChildPath(normalizedSourceRoot, normalizedTargetRoot))
        {
            _logger.LogWarning(
                "SmartBP module move marker contains unsafe source/target pair. SourceRoot={SourceRoot}, TargetRoot={TargetRoot}",
                normalizedSourceRoot,
                normalizedTargetRoot);
            File.Delete(MovePendingFilePath);
            return;
        }

        try
        {
            if (Directory.Exists(normalizedSourceRoot))
            {
                Directory.Delete(normalizedSourceRoot, recursive: true);
                _logger.LogInformation(
                    "Deleted old SmartBP module directory after successful target load. SourceRoot={SourceRoot}, TargetRoot={TargetRoot}",
                    normalizedSourceRoot,
                    normalizedTargetRoot);
            }

            File.Delete(MovePendingFilePath);
        }
        catch (Exception ex)
        {
            pending.LastCleanupError = FormatExceptionForUser(ex);
            WriteMovePendingState(pending);
            _logger.LogWarning(
                ex,
                "SmartBP module target loaded, but old directory cleanup is still pending. SourceRoot={SourceRoot}, TargetRoot={TargetRoot}",
                normalizedSourceRoot,
                normalizedTargetRoot);
        }
    }

    /// <summary>
    /// 将旧 Documents 位置中的旧版 OCR 模型目录一次性迁移到 SmartBP 模块根目录。
    /// </summary>
    /// <param name="moduleRoot">当前 SmartBP 模块根目录。</param>
    /// <returns>迁移状态记录完成后结束的任务。</returns>
    private async Task MigrateLegacyOcrModelsOnceAsync(string moduleRoot)
    {
        var state = ReadState();
        if (state?.LegacyOcrModelMigration.Completed == true)
        {
            _logger.LogDebug("Skipping legacy OCR model migration because it has already completed.");
            return;
        }

        var oldRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "neo-bpsys-wpf", "OCRModels");
        var migration = new SmartBpLegacyOcrModelMigrationState();
        if (!Directory.Exists(oldRoot))
        {
            _logger.LogInformation("No legacy OCR model root found. OldRoot={OldRoot}", oldRoot);
            migration.Completed = true;
            migration.Reason = "NoLegacyModels";
            WriteState(new SmartBpModuleState { ModuleRoot = moduleRoot, LegacyOcrModelMigration = migration });
            return;
        }

        var newRoot = Path.Combine(moduleRoot, "OCRModels");
        Directory.CreateDirectory(newRoot);
        foreach (var key in KnownOcrModelKeys)
        {
            var modelDir = Path.Combine(oldRoot, key);
            if (!Directory.Exists(modelDir))
                continue;

            if (!IsLegacyModelReady(modelDir))
            {
                _logger.LogDebug("Skipping incomplete legacy OCR model. ModelKey={ModelKey}, ModelDir={ModelDir}", key, modelDir);
                continue;
            }
            var staging = Path.Combine(newRoot, $"{key}.staging");
            var target = Path.Combine(newRoot, key);
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
            CopyDirectory(modelDir, staging);
            if (!IsLegacyModelReady(staging))
            {
                _logger.LogWarning("Legacy OCR model copy verification failed. ModelKey={ModelKey}, Staging={Staging}", key, staging);
                Directory.Delete(staging, recursive: true);
                continue;
            }
            if (Directory.Exists(target))
                Directory.Delete(target, recursive: true);
            Directory.Move(staging, target);
            try
            {
                Directory.Delete(modelDir, recursive: true);
                _logger.LogInformation("Migrated legacy OCR model. ModelKey={ModelKey}, Target={Target}", key, target);
            }
            catch
            {
                _logger.LogWarning("Legacy OCR model migrated but cleanup is pending. ModelKey={ModelKey}, ModelDir={ModelDir}", key, modelDir);
                migration.PendingCleanupModelKeys.Add(key);
            }
        }

        migration.Completed = true;
        migration.Reason = migration.PendingCleanupModelKeys.Count == 0 ? "Completed" : "PendingCleanup";
        WriteState(new SmartBpModuleState { ModuleRoot = moduleRoot, LegacyOcrModelMigration = migration });
        await Task.CompletedTask;
    }

    /// <summary>
    /// 判断旧版 PaddleOCR 模型目录是否包含所有必需的推理组件。
    /// </summary>
    /// <param name="modelRoot">Legacy model root directory.</param>
    /// <returns><see langword="true"/> when det, cls, and rec components are present.</returns>
    private static bool IsLegacyModelReady(string modelRoot) =>
        new[] { "det", "cls", "rec" }.All(component =>
        {
            var dir = Path.Combine(modelRoot, component);
            return File.Exists(Path.Combine(dir, "inference.pdiparams")) &&
                   (File.Exists(Path.Combine(dir, "inference.pdmodel")) ||
                    File.Exists(Path.Combine(dir, "inference.json")));
        });

    /// <summary>
    /// 递归复制目录树并保留相对路径。
    /// </summary>
    /// <param name="source">源目录。</param>
    /// <param name="target">目标目录。</param>
    private static void CopyDirectory(string source, string target)
    {
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, directory)));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }

    /// <summary>
    /// 替换模块文件，同时保留运行时托管的 OCR 和 AI 资产目录。
    /// </summary>
    /// <param name="sourceRoot">Validated replacement module root.</param>
    /// <param name="targetRoot">Existing module root to update.</param>
    private void ReplaceModuleRootPreservingManagedAssets(string sourceRoot, string targetRoot)
    {
        var normalizedSourceRoot = Path.GetFullPath(sourceRoot);
        var normalizedTargetRoot = Path.GetFullPath(targetRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(normalizedTargetRoot)!);

        if (!Directory.Exists(normalizedTargetRoot))
        {
            Directory.Move(normalizedSourceRoot, normalizedTargetRoot);
            return;
        }

        _logger.LogInformation(
            "Replacing SmartBP module target root while preserving managed model assets. TargetRoot={TargetRoot}",
            normalizedTargetRoot);

        foreach (var entry in Directory.EnumerateFileSystemEntries(normalizedTargetRoot).ToArray())
        {
            if (IsManagedAssetRoot(entry))
            {
                _logger.LogDebug("Preserving SmartBP managed asset directory during module replacement: {Path}", entry);
                continue;
            }

            DeleteFileSystemEntry(entry);
        }

        foreach (var entry in Directory.EnumerateFileSystemEntries(normalizedSourceRoot).ToArray())
        {
            var name = Path.GetFileName(entry);
            var destination = Path.Combine(normalizedTargetRoot, name);
            if (ManagedAssetRootNames.Contains(name) && Directory.Exists(destination))
            {
                _logger.LogInformation(
                    "Skipping packaged SmartBP managed asset directory because an existing downloaded asset directory is present. Path={Path}",
                    destination);
                continue;
            }

            if (Directory.Exists(destination) || File.Exists(destination))
            {
                DeleteFileSystemEntry(destination);
            }

            if (Directory.Exists(entry))
            {
                Directory.Move(entry, destination);
            }
            else
            {
                File.Move(entry, destination, overwrite: true);
            }
        }
    }

    /// <summary>
    /// 判断路径是否属于 SmartBP 运行时托管资产根目录之一。
    /// </summary>
    /// <param name="path">要检查的路径。</param>
    /// <returns><see langword="true"/> when the path is a known managed asset directory.</returns>
    private static bool IsManagedAssetRoot(string path)
    {
        return Directory.Exists(path) && ManagedAssetRootNames.Contains(Path.GetFileName(path));
    }

    /// <summary>
    /// 当文件系统项是已存在的文件或目录时删除它。
    /// </summary>
    /// <param name="path">要删除的文件或目录路径。</param>
    private static void DeleteFileSystemEntry(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
            return;
        }

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// 在完整路径标准化后，判断一个路径是否等于或嵌套在另一个路径下。
    /// </summary>
    /// <param name="child">Candidate child path.</param>
    /// <param name="parent">Candidate parent path.</param>
    /// <returns><see langword="true"/> when <paramref name="child"/> is the same as or below <paramref name="parent"/>.</returns>
    private static bool IsSameOrChildPath(string child, string parent)
    {
        var normalizedChild = Path.GetFullPath(child)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedParent = Path.GetFullPath(parent)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return normalizedChild.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 通过在最近的已存在目录中创建临时探测文件检查写入权限。
    /// </summary>
    /// <param name="path">Requested module path or parent path.</param>
    /// <returns><see langword="true"/> when a temporary file can be created.</returns>
    private static bool HasWriteAccess(string path)
    {
        try
        {
            var probeDirectory = Directory.Exists(path)
                ? path
                : GetNearestExistingDirectory(path);
            if (string.IsNullOrWhiteSpace(probeDirectory))
                return false;

            var probePath = Path.Combine(probeDirectory, $".smartbp-write-test-{Guid.NewGuid():N}.tmp");
            using (File.Create(probePath, 1, FileOptions.DeleteOnClose))
            {
            }

            if (File.Exists(probePath))
                File.Delete(probePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 从给定路径向上查找，直到找到已存在的目录。
    /// </summary>
    /// <param name="path">可能尚不存在的路径。</param>
    /// <returns>最近的已存在目录；找不到时返回 <see langword="null"/>。</returns>
    private static string? GetNearestExistingDirectory(string path)
    {
        var current = Path.GetDirectoryName(Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (Directory.Exists(current))
                return current;

            current = Path.GetDirectoryName(current);
        }

        return null;
    }

    /// <summary>
    /// 报告当前程序集是否使用 DEBUG 符号编译。
    /// </summary>
    /// <returns><see langword="true"/> in debug builds.</returns>
    private static bool IsDebugBuild()
    {
#if DEBUG
        return true;
#else
        return false;
#endif
    }

    /// <summary>
    /// 报告当前程序集是否使用 PREVIEW 符号编译。
    /// </summary>
    /// <returns><see langword="true"/> in preview builds.</returns>
    private static bool IsPreviewBuild()
    {
#if PREVIEW
        return true;
#else
        return false;
#endif
    }

    /// <summary>
    /// 获取与当前应用版本匹配的发布标签中附带的 SmartBP 模块 manifest。
    /// </summary>
    /// <returns>要求的模块 manifest；无法获取发布元数据时返回 <see langword="null"/>。</returns>
    private async Task<SmartBpModuleManifest?> TryFetchCurrentTagManifestAsync()
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(AppConstants.AppName);
            var releaseResponse = await httpClient.GetAsync(ReleaseApiUrl);
            releaseResponse.EnsureSuccessStatusCode();
            var releaseJson = await releaseResponse.Content.ReadAsStringAsync();
            var releases = JsonSerializer.Deserialize<ReleaseInfo[]>(releaseJson, JsonOptions) ?? [];
            _logger.LogInformation("Fetched SmartBP release metadata from forwarded API. ReleaseCount={ReleaseCount}", releases.Length);
            var currentRelease = releases.FirstOrDefault(release =>
                string.Equals(release.TagName, AppConstants.AppVersion, StringComparison.OrdinalIgnoreCase));
            if (currentRelease == null)
            {
                _logger.LogWarning(
                    "SmartBP module release tag was not found from forwarded release API. Tag={Tag}",
                    AppConstants.AppVersion);
                return null;
            }

            var manifestAsset = currentRelease.Assets.FirstOrDefault(asset =>
                string.Equals(asset.Name, ModuleManifestAssetName, StringComparison.OrdinalIgnoreCase));
            if (manifestAsset == null || string.IsNullOrWhiteSpace(manifestAsset.BrowserDownloadUrl))
            {
                _logger.LogWarning(
                    "SmartBP module manifest asset was not found from forwarded release API. Tag={Tag}",
                    AppConstants.AppVersion);
                return null;
            }

            var url = ModuleManifestDownloadMirror + manifestAsset.BrowserDownloadUrl;
            _logger.LogInformation("Downloading SmartBP module manifest. Url={Url}", url);
            var json = await httpClient.GetStringAsync(url);
            return JsonSerializer.Deserialize<SmartBpModuleManifest>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch SmartBP module manifest for current app tag. Local module use is not blocked.");
            return null;
        }
    }

    /// <summary>
    /// 为中文用户应用已配置的 GitHub 代理镜像。
    /// </summary>
    /// <param name="url">原始下载 URL。</param>
    /// <returns>已配置镜像时返回镜像 URL；否则返回原始 URL。</returns>
    private string GetMirroredDownloadUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return url;

        if (!_settingsHostService.Settings.CultureInfo.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            return url;

        var mirror = _settingsHostService.Settings.GhProxyMirror;
        return string.IsNullOrWhiteSpace(mirror) ? url : mirror + url;
    }

    /// <summary>
    /// 将异常链扁平化为紧凑的用户可读诊断字符串。
    /// </summary>
    /// <param name="exception">要格式化的异常。</param>
    /// <returns>去重后的消息摘要。</returns>
    private static string FormatExceptionForUser(Exception exception)
    {
        var messages = new List<string>();
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (!string.IsNullOrWhiteSpace(current.Message))
                messages.Add(current.Message);
        }

        if (exception is ReflectionTypeLoadException reflectionTypeLoadException)
        {
            messages.AddRange(reflectionTypeLoadException.LoaderExceptions
                .Where(e => !string.IsNullOrWhiteSpace(e?.Message))
                .Select(e => e!.Message));
        }

        return string.Join(" | ", messages.Distinct(StringComparer.Ordinal));
    }

    /// <summary>
    /// 在 SmartBP 模块 native 探测目录中查找非托管库。
    /// </summary>
    /// <param name="moduleRoot">SmartBP module root.</param>
    /// <param name="libraryName">库文件名，可带或不带 .dll 扩展名。</param>
    /// <returns>匹配的库路径；未找到时返回 <see langword="null"/>。</returns>
    internal static string? FindModuleUnmanagedLibraryPath(string moduleRoot, string libraryName)
    {
        if (string.IsNullOrWhiteSpace(moduleRoot) || string.IsNullOrWhiteSpace(libraryName))
            return null;

        var safeLibraryName = Path.GetFileName(libraryName);
        if (!string.Equals(safeLibraryName, libraryName, StringComparison.Ordinal))
            return null;

        var fileName = Path.HasExtension(safeLibraryName) ? safeLibraryName : $"{safeLibraryName}.dll";
        var candidates = GetModuleNativeSearchDirectories(moduleRoot)
            .Select(directory => Path.Combine(directory, fileName));

        return candidates.FirstOrDefault(File.Exists);
    }

    /// <summary>
    /// 为 Windows DLL 解析注册 SmartBP native 目录，并预加载已知依赖锚点。
    /// </summary>
    /// <param name="moduleRoot">SmartBP module root.</param>
    /// <param name="logger">Optional logger for native probing diagnostics.</param>
    internal static void RegisterModuleNativeSearchDirectories(
        string moduleRoot,
        ILogger? logger = null)
    {
        lock (NativeSearchPathSync)
        {
            foreach (var directory in GetModuleNativeSearchDirectories(moduleRoot).Where(Directory.Exists))
            {
                if (!RegisteredNativeSearchDirectories.Add(directory)) continue;

                var cookie = AddDllDirectory(directory);
                if (cookie == IntPtr.Zero)
                {
                    logger?.LogDebug(
                        "Failed to add SmartBP module native search directory through AddDllDirectory. Directory={Directory}, Error={Error}",
                        directory,
                        Marshal.GetLastWin32Error());
                }
                else
                {
                    logger?.LogDebug("Added SmartBP module native search directory: {Directory}", directory);
                }

                // AddDllDirectory 帮助 P/Invoke；PATH 也能帮助仍使用旧版 Windows
                // 搜索语义加载自身依赖的 native 库。
                PrependProcessPath(directory);
            }

            PreloadModuleNativeLibraries(moduleRoot, logger);
        }
    }

    /// <summary>
    /// 预加载依赖解析顺序敏感的已知 native 库。
    /// </summary>
    /// <param name="moduleRoot">SmartBP module root.</param>
    /// <param name="logger">Optional logger for preload diagnostics.</param>
    private static void PreloadModuleNativeLibraries(
        string moduleRoot,
        ILogger? logger)
    {
        foreach (var libraryPath in GetKnownModuleNativeLibrariesToPreload(moduleRoot))
        {
            if (PreloadedNativeLibraries.ContainsKey(libraryPath)) continue;

            try
            {
                var handle = NativeLibrary.Load(libraryPath);
                PreloadedNativeLibraries[libraryPath] = handle;
                logger?.LogDebug("Preloaded SmartBP module native library: {LibraryPath}", libraryPath);
            }
            catch (Exception ex)
            {
                logger?.LogDebug(
                    ex,
                    "Failed to preload SmartBP module native library. LibraryPath={LibraryPath}",
                    libraryPath);
            }
        }
    }

    /// <summary>
    /// 枚举在托管模块代码运行前应提前加载的 native 库。
    /// </summary>
    /// <param name="moduleRoot">SmartBP module root.</param>
    /// <returns>Existing library paths in dependency-friendly order.</returns>
    private static IEnumerable<string> GetKnownModuleNativeLibrariesToPreload(string moduleRoot)
    {
        var nativeDirectories = GetModuleNativeSearchDirectories(moduleRoot).Where(Directory.Exists).ToArray();
        var orderedNames = new[]
        {
            "onnxruntime_providers_shared.dll",
            "onnxruntime.dll"
        };

        foreach (var name in orderedNames)
        {
            foreach (var directory in nativeDirectories)
            {
                var path = Path.Combine(directory, name);
                if (File.Exists(path)) yield return path;
            }
        }
    }

    /// <summary>
    /// 构建 SmartBP 模块内 native 探测目录的有序集合。
    /// </summary>
    /// <param name="moduleRoot">SmartBP module root.</param>
    /// <returns>Native probing directories, including RID-specific and module-root fallbacks.</returns>
    private static IReadOnlyList<string> GetModuleNativeSearchDirectories(string moduleRoot)
    {
        if (string.IsNullOrWhiteSpace(moduleRoot)) return [];

        var directories = new List<string>
        {
            Path.Combine(moduleRoot, "runtimes", SmartBpModuleConstants.Rid, "native"),
            moduleRoot
        };

        var runtimesRoot = Path.Combine(moduleRoot, "runtimes");
        if (Directory.Exists(runtimesRoot))
        {
            foreach (var runtimeDirectory in Directory.EnumerateDirectories(runtimesRoot))
            {
                var nativeDirectory = Path.Combine(runtimeDirectory, "native");
                if (!directories.Contains(nativeDirectory, StringComparer.OrdinalIgnoreCase))
                    directories.Add(nativeDirectory);
            }
        }

        return directories;
    }

    /// <summary>
    /// 当 native 探测目录尚未存在于进程 PATH 中时，将其前置加入 PATH。
    /// </summary>
    /// <param name="directory">要添加的目录。</param>
    private static void PrependProcessPath(string directory)
    {
        var current = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var paths = current.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (paths.Any(path => string.Equals(path, directory, StringComparison.OrdinalIgnoreCase))) return;
        Environment.SetEnvironmentVariable("PATH", string.IsNullOrWhiteSpace(current)
            ? directory
            : $"{directory}{Path.PathSeparator}{current}");
    }

    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr AddDllDirectory(string newDirectory);

}
