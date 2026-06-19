using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.SmartBpModule;

namespace neo_bpsys_wpf.Services.SmartBpModule;

/// <summary>
/// Loads, validates, installs, and persists the SmartBP module.
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

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SmartBpModuleManager> _logger;
    private readonly ISettingsHostService _settingsHostService;
    private readonly IArchiveService _archiveService;
    private ISmartBpModuleEntryPoint? _entryPoint;
    private IReadOnlyList<SmartBpFeatureCommand> _featureCommands = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="SmartBpModuleManager"/> class.
    /// </summary>
    /// <param name="serviceProvider">Host service provider.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="settingsHostService">Settings host service.</param>
    /// <param name="archiveService">Archive extraction service.</param>
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
    /// Raised when module load state changes.
    /// </summary>
    public event EventHandler? ModuleStateChanged;

    /// <summary>
    /// Loaded module content object.
    /// </summary>
    public object? ModuleContent { get; private set; }

    /// <summary>
    /// Current module root.
    /// </summary>
    public string ModuleRoot { get; private set; }

    /// <summary>
    /// Whether the module is loaded.
    /// </summary>
    public bool IsModuleLoaded => _entryPoint != null && ModuleContent != null;

    /// <summary>
    /// Last module load or validation failure message.
    /// </summary>
    public string LastFailureMessage { get; private set; } = string.Empty;

    /// <summary>
    /// Whether the last module archive import was staged and requires an application restart to finish.
    /// </summary>
    public bool IsRestartRequiredForPendingModuleImport { get; private set; }

    /// <summary>
    /// State file path.
    /// </summary>
    public static string StateFilePath => Path.Combine(AppConstants.AppDataPath, "SmartBpModuleState.json");

    /// <summary>
    /// Pending module directory migration marker file path.
    /// </summary>
    public static string MovePendingFilePath => Path.Combine(AppConstants.AppDataPath, "SmartBpModuleMovePending.json");

    /// <summary>
    /// Default module root path.
    /// </summary>
    public static string GetDefaultModuleRoot() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "neo-bpsys-wpf", "Components", "SmartBpModule");

    /// <summary>
    /// Reads persisted state.
    /// </summary>
    /// <returns>Persisted state, or null.</returns>
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
    /// Gets the preferred module root for UI display and startup loading.
    /// </summary>
    /// <returns>Preferred module root.</returns>
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
    /// Determines whether a saved module root or pending migration target exists.
    /// </summary>
    /// <returns>True when a persisted module root preference exists.</returns>
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
    /// Persists the preferred SmartBP module root without marking the module as successfully loaded.
    /// </summary>
    /// <param name="moduleRoot">Preferred module root path.</param>
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
    /// Updates the preferred SmartBP module directory by copying the current module through staging when an existing module is available.
    /// </summary>
    /// <param name="targetRoot">Target module root.</param>
    /// <returns>True when the target was prepared and persisted.</returns>
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
    /// Loads the persisted module if available.
    /// </summary>
    /// <returns>True when loaded.</returns>
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
    /// Loads a module from a directory.
    /// </summary>
    /// <param name="moduleRoot">Module root.</param>
    /// <param name="installKind">Install kind.</param>
    /// <returns>True when loaded.</returns>
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
                    // Continue with module-local probing for SmartBP-owned dependencies.
                }

                var candidate = Path.Combine(moduleRoot, $"{name.Name}.dll");
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
    /// Downloads the current app tag SmartBP module package and installs it through a staging directory.
    /// </summary>
    /// <param name="targetRoot">Final target root.</param>
    /// <param name="progress">Optional progress reporter from 0 to 100.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when downloaded and installed or staged for restart.</returns>
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
    /// Imports a module archive through a staging directory.
    /// </summary>
    /// <param name="archivePath">Archive path.</param>
    /// <param name="targetRoot">Final target root.</param>
    /// <returns>True when imported and loaded or staged for restart.</returns>
    public async Task<bool> ImportArchiveAsync(string archivePath, string targetRoot)
    {
        return await ImportArchiveAsync(archivePath, targetRoot, "PreviewArchiveImport");
    }

    /// <summary>
    /// Imports a module archive through a staging directory.
    /// </summary>
    /// <param name="archivePath">Archive path.</param>
    /// <param name="targetRoot">Final target root.</param>
    /// <param name="installKind">Install kind persisted in module state.</param>
    /// <returns>True when imported and loaded or staged for restart.</returns>
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
                if (Directory.Exists(normalizedTargetRoot))
                {
                    _logger.LogInformation("Replacing existing SmartBP module target root: {TargetRoot}", normalizedTargetRoot);
                    Directory.Delete(normalizedTargetRoot, recursive: true);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(normalizedTargetRoot)!);
                Directory.Move(candidateRoot, normalizedTargetRoot);
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
    /// Executes a feature command.
    /// </summary>
    /// <param name="commandId">Command id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Asynchronous task.</returns>
    public Task ExecuteFeatureCommandAsync(string commandId, CancellationToken cancellationToken)
    {
        var command = _featureCommands.FirstOrDefault(c => c.CommandId == commandId);
        return command?.ExecuteAsync(cancellationToken) ?? Task.CompletedTask;
    }

    /// <summary>
    /// Validates a module directory.
    /// </summary>
    /// <param name="moduleRoot">Module root.</param>
    /// <param name="allowDevelopmentDirectory">Whether debug development directory is allowed.</param>
    /// <param name="manifest">Component manifest.</param>
    /// <param name="error">Validation error.</param>
    /// <returns>Validation result.</returns>
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
    /// Determines whether a path is unsafe for module installation.
    /// </summary>
    /// <param name="path">Candidate path.</param>
    /// <returns>True when unsafe.</returns>
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
    /// Computes a file SHA-256 hash.
    /// </summary>
    /// <param name="filePath">File path.</param>
    /// <returns>Lower-case SHA-256 hash.</returns>
    public static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    /// <summary>
    /// Fetches the SmartBP module manifest for the current app release tag.
    /// </summary>
    /// <returns>Required module manifest, or null when offline, unavailable, or not required.</returns>
    public async Task<SmartBpModuleManifest?> TryFetchRequiredModuleManifestAsync()
    {
        if (IsDebugBuild() || IsPreviewBuild())
            return null;

        return await TryFetchCurrentTagManifestAsync();
    }

    /// <summary>
    /// Determines whether a local module version satisfies the required module version.
    /// </summary>
    /// <param name="localVersion">Local module version.</param>
    /// <param name="requiredVersion">Required module version.</param>
    /// <returns>True when local version is equal or newer.</returns>
    public static bool IsModuleVersionAllowed(string localVersion, string requiredVersion)
    {
        if (Version.TryParse(localVersion.Replace('-', '.'), out var local) &&
            Version.TryParse(requiredVersion.Replace('-', '.'), out var required))
        {
            return local >= required;
        }

        return string.Compare(localVersion, requiredVersion, StringComparison.OrdinalIgnoreCase) >= 0;
    }

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

            if (Directory.Exists(targetRoot))
            {
                _logger.LogInformation(
                    "Replacing SmartBP module target from pending archive import. TargetRoot={TargetRoot}, PreparedRoot={PreparedRoot}",
                    targetRoot,
                    preparedRoot);
                Directory.Delete(targetRoot, recursive: true);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetRoot)!);
            Directory.Move(preparedRoot, targetRoot);
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

    private static bool IsLegacyModelReady(string modelRoot) =>
        new[] { "det", "cls", "rec" }.All(component =>
        {
            var dir = Path.Combine(modelRoot, component);
            return File.Exists(Path.Combine(dir, "inference.pdiparams")) &&
                   (File.Exists(Path.Combine(dir, "inference.pdmodel")) ||
                    File.Exists(Path.Combine(dir, "inference.json")));
        });

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

    private static bool IsSameOrChildPath(string child, string parent)
    {
        var normalizedChild = Path.GetFullPath(child)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedParent = Path.GetFullPath(parent)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return normalizedChild.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
    }

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

    private static bool IsDebugBuild()
    {
#if DEBUG
        return true;
#else
        return false;
#endif
    }

    private static bool IsPreviewBuild()
    {
#if PREVIEW
        return true;
#else
        return false;
#endif
    }

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

    private string GetMirroredDownloadUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return url;

        var mirror = _settingsHostService.Settings.GhProxyMirror;
        return string.IsNullOrWhiteSpace(mirror) ? url : mirror + url;
    }

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

    internal static string? FindModuleUnmanagedLibraryPath(string moduleRoot, string libraryName)
    {
        if (string.IsNullOrWhiteSpace(moduleRoot) || string.IsNullOrWhiteSpace(libraryName))
            return null;

        var safeLibraryName = Path.GetFileName(libraryName);
        if (!string.Equals(safeLibraryName, libraryName, StringComparison.Ordinal))
            return null;

        var fileName = Path.HasExtension(safeLibraryName) ? safeLibraryName : $"{safeLibraryName}.dll";
        var candidates = new[]
        {
            Path.Combine(moduleRoot, "runtimes", SmartBpModuleConstants.Rid, "native", fileName),
            Path.Combine(moduleRoot, fileName)
        };

        return candidates.FirstOrDefault(File.Exists);
    }

}
