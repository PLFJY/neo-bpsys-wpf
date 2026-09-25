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
/// SmartBP 模块管理器的Loading逻辑。
/// </summary>
public sealed partial class SmartBpModuleManager
{
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

        if (pending != null)
        {
            TryCopyManagedAssetsBeforeTargetLoad(pending);
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
        var loaded = await LoadModuleFromDirectoryAsync(moduleRoot, installKind);
        return loaded;
    }

    /// <summary>
    /// 从目录加载模块。
    /// </summary>
    /// <param name="moduleRoot">模块根目录。</param>
    /// <param name="installKind">安装类型。</param>
    /// <returns>成功加载时返回 <see langword="true"/>。</returns>
    public async Task<bool> LoadModuleFromDirectoryAsync(string moduleRoot, string installKind = "LocalDirectory")
    {
        _logger.LogInformation(
            "Loading SmartBP module from directory. ModuleRoot={ModuleRoot}, InstallKind={InstallKind}",
            moduleRoot,
            installKind);

        _isModuleVersionOutdated = false;
        RequiredModuleVersion = null;

        if (!ValidateModuleDirectory(moduleRoot, allowDevelopmentDirectory: IsDebugBuild(), out var manifest, out var error))
        {
            LastFailureMessage = error;
            _logger.LogWarning("SmartBP module validation failed: {Error}", error);
            return false;
        }

        // 远程版本检查不再阻塞加载流程：ABI 兼容性已由 ValidateModuleDirectory 硬性校验，
        // 本地模块版本是否过时只作为更新提示，在加载成功后异步通知。
        var entryAssembly = Path.Combine(moduleRoot, SmartBpModuleConstants.EntryAssemblyName);
        try
        {
            var loadContext = new AssemblyLoadContext($"SmartBpModule-{Guid.NewGuid():N}", isCollectible: false);
            RegisterModuleNativeSearchDirectories(moduleRoot, _logger);
            loadContext.Resolving += (_, name) =>
            {
                _logger.LogDebug("Resolving SmartBP module assembly: {AssemblyName}", name.FullName);

                // 卫星资源程序集（.resources）的 culture 回退探测会遍历数百种
                // culture，每次都会触发 Resolving 回调。对这类程序集直接走文件探测，
                // 跳过 LoadFromAssemblyName 调用——后者必然抛出 FileNotFoundException
                // 并被 catch 吞掉，但 first-chance exception 会被调试器输出，在
                // Visual Studio 中造成严重卡顿。
                var isResourceSatellite = name.Name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase);

                if (!isResourceSatellite)
                {
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
                }

                var candidate = string.IsNullOrWhiteSpace(name.CultureName)
                    ? Path.Combine(moduleRoot, $"{name.Name}.dll")
                    : Path.Combine(moduleRoot, name.CultureName, $"{name.Name}.dll");
                if (!File.Exists(candidate))
                {
                    if (!isResourceSatellite)
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
                    if (IsOptionalPaddleBackendLibrary(libraryName))
                    {
                        _logger.LogDebug(
                            "SmartBP module optional Paddle backend library was not found. LibraryName={LibraryName}, ModuleRoot={ModuleRoot}",
                            libraryName,
                            moduleRoot);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "SmartBP module native dependency was not found. LibraryName={LibraryName}, ModuleRoot={ModuleRoot}",
                            libraryName,
                            moduleRoot);
                    }
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
            // 程序集加载、类型查找、入口点实例化和教程注册不依赖 UI 线程，
            // 放到后台线程执行避免阻塞 UI；同时这些步骤会触发大量程序集解析
            // 的 first-chance FileNotFoundException，在 VS 调试器中输出会拖
            // 慢 UI 线程响应。后台执行让 UI 保持响应。
            var entryPoint = await Task.Run(() =>
            {
                var assembly = loadContext.LoadFromAssemblyPath(entryAssembly);
                var entryType = assembly.GetTypes()
                    .FirstOrDefault(t => typeof(ISmartBpModuleEntryPoint).IsAssignableFrom(t) && !t.IsAbstract);
                if (entryType == null)
                {
                    LastFailureMessage = "Module entry type was not found.";
                    _logger.LogWarning("SmartBP module entry type was not found in assembly: {EntryAssembly}", entryAssembly);
                    return (ISmartBpModuleEntryPoint?)null;
                }

                _logger.LogInformation("Creating SmartBP module entry point. EntryType={EntryType}", entryType.FullName);
                var ep = (ISmartBpModuleEntryPoint)Activator.CreateInstance(entryType)!;
                ModuleRoot = moduleRoot;

                if (ep is ITutorialRegistrationContributor contributor)
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
                return ep;
            }).ConfigureAwait(true);

            if (entryPoint == null)
                return false;

            _entryPoint = entryPoint;

            // CreateSmartBpContent 创建 WPF UI 元素（InitializeComponent/BAML 解析），
            // 必须在 UI 线程执行。
            _logger.LogInformation("Creating SmartBP module content.");
            ModuleContent = _entryPoint.CreateSmartBpContent(_serviceProvider);
            _featureCommands = _entryPoint.GetFeatureCommands();
            _postGameRecognitionProgressSource = _entryPoint.GetPostGameRecognitionProgressSource();
            if (_postGameRecognitionProgressSource is not null)
                _postGameRecognitionProgressSource.ProgressChanged += OnPostGameRecognitionProgressChanged;
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
            // 加载成功后异步检查远程版本是否过时；不阻塞加载流程，结果通过事件通知。
            _ = NotifyModuleVersionOutdatedIfNeededAsync(manifest?.ModuleVersion);
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
    /// 异步比较本地模块版本与远程发布标签要求的版本，过旧时设置版本过旧状态、
    /// 触发 <see cref="ModuleVersionOutdated"/> 事件并通过 <see cref="ModuleStateChanged"/>
    /// 通知全局表现为模块未加载。
    /// </summary>
    /// <param name="localVersion">已加载模块的本地版本号；为空时跳过检查。</param>
    /// <returns>异步检查完成后结束的任务。</returns>
    private async Task NotifyModuleVersionOutdatedIfNeededAsync(string? localVersion)
    {
        if (string.IsNullOrWhiteSpace(localVersion))
            return;

        if (IsDebugBuild() || IsPreviewBuild())
            return;

        var requiredManifest = await TryFetchRequiredModuleManifestAsync();
        if (requiredManifest == null)
            return;

        if (!IsModuleVersionAllowed(localVersion, requiredManifest.ModuleVersion))
        {
            _logger.LogWarning(
                "SmartBP module version is too old. Local={LocalVersion}, Required={RequiredVersion}",
                localVersion,
                requiredManifest.ModuleVersion);
            _isModuleVersionOutdated = true;
            RequiredModuleVersion = requiredManifest.ModuleVersion;
            ModuleVersionOutdated?.Invoke(this, new ModuleVersionOutdatedEventArgs(localVersion, requiredManifest.ModuleVersion));
            ModuleStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

}
