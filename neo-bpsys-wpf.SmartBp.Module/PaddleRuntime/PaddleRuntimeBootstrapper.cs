using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.SmartBp.Module.PaddleRuntime;

/// <summary>
/// <see cref="IPaddleRuntimeBootstrapper"/> 的实现。在 SmartBP 模块加载前、
/// 任何 PaddleInference native P/Invoke 前选择并加载唯一的 native runtime（CPU 或 CUDA）。
/// </summary>
public sealed class PaddleRuntimeBootstrapper : IPaddleRuntimeBootstrapper
{
    /// <summary>
    /// PaddleInference native 入口模块文件名。
    /// </summary>
    private const string NativeModuleName = "paddle_inference_c.dll";

    /// <summary>
    /// 指示 <see cref="LoadLibraryExW"/> 在加载目标 DLL 时，同时把该 DLL 所在目录加入依赖项搜索路径。
    /// </summary>
    private const uint LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR = 0x100;

    /// <summary>
    /// 指示 DLL 搜索时应包含通过 <see cref="AddDllDirectory"/> 注册的用户目录。
    /// </summary>
    private const uint LOAD_LIBRARY_SEARCH_USER_DIRS = 0x400;

    /// <summary>
    /// 指示 DLL 搜索时应包含 system32 目录。
    /// </summary>
    private const uint LOAD_LIBRARY_SEARCH_SYSTEM32 = 0x800;

    private readonly ISettingsHostService _settingsHost;
    private readonly ICudaDeviceDetector _cudaDetector;
    private readonly IPaddleRuntimeManifestProvider _manifestProvider;
    private readonly IPaddleRuntimeComponentService _componentService;
    private readonly IPaddleCudaPrerequisiteSetupService _prerequisiteSetupService;
    private readonly ISmartBpModuleStorageProvider _moduleStorage;
    private readonly PaddleRuntimeState _state;
    private readonly ILogger<PaddleRuntimeBootstrapper> _logger;

    /// <summary>
    /// 初始化 <see cref="PaddleRuntimeBootstrapper"/> 类的新实例。
    /// </summary>
    /// <param name="settingsHost">应用设置宿主服务，用于读取用户偏好与历史 CUDA 故障记录。</param>
    /// <param name="cudaDetector">CUDA 设备检测器，用于枚举系统 NVIDIA GPU。</param>
    /// <param name="manifestProvider">Paddle runtime manifest 提供者，用于按 Compute Capability 解析 CUDA 包。</param>
    /// <param name="componentService">Paddle CUDA runtime 组件管理服务，用于查询已安装的 CUDA runtime 状态。</param>
    /// <param name="prerequisiteSetupService">CUDA/cuDNN 可再发行依赖管理服务。</param>
    /// <param name="moduleStorage">SmartBP 模块存储提供者，用于解析模块自带的 CPU runtime。</param>
    /// <param name="state">Paddle runtime 运行时状态，由 Bootstrap 在启动时写入。</param>
    /// <param name="logger">日志记录器。</param>
    /// <exception cref="ArgumentNullException">任一参数为 <see langword="null"/>。</exception>
    public PaddleRuntimeBootstrapper(
        ISettingsHostService settingsHost,
        ICudaDeviceDetector cudaDetector,
        IPaddleRuntimeManifestProvider manifestProvider,
        IPaddleRuntimeComponentService componentService,
        IPaddleCudaPrerequisiteSetupService prerequisiteSetupService,
        ISmartBpModuleStorageProvider moduleStorage,
        IPaddleRuntimeState state,
        ILogger<PaddleRuntimeBootstrapper> logger)
    {
        _settingsHost = settingsHost ?? throw new ArgumentNullException(nameof(settingsHost));
        _cudaDetector = cudaDetector ?? throw new ArgumentNullException(nameof(cudaDetector));
        _manifestProvider = manifestProvider ?? throw new ArgumentNullException(nameof(manifestProvider));
        _componentService = componentService ?? throw new ArgumentNullException(nameof(componentService));
        _prerequisiteSetupService = prerequisiteSetupService ?? throw new ArgumentNullException(nameof(prerequisiteSetupService));
        _moduleStorage = moduleStorage ?? throw new ArgumentNullException(nameof(moduleStorage));
        _state = (PaddleRuntimeState)(state ?? throw new ArgumentNullException(nameof(state)));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public void Bootstrap(bool forceCpu)
    {
        var settings = _settingsHost.Settings;
        var preferredBackend = settings.PreferredOcrBackend;

        _logger.LogInformation(
            "Paddle runtime bootstrap starting. ForceCpu={ForceCpu}, PreferredBackend={PreferredBackend}, ProcessArchitecture={ProcessArchitecture}",
            forceCpu,
            preferredBackend,
            RuntimeInformation.ProcessArchitecture);

        // 1. 命令行强制 CPU
        if (forceCpu)
        {
            _logger.LogInformation("Bootstrap forced to CPU by command line argument --force-cpu-ocr.");
            LoadCpuAndCommit(
                devices: null,
                selectedDevice: null,
                cudaInstalled: false,
                cudaCompatible: false,
                error: null);
            return;
        }

        // 2. 一次性 CPU 强制保护（防止 CUDA 故障重启死循环）。
        //    仅在本次启动消费，消费后立即置回 false，下次启动恢复正常 CUDA 尝试。
        //    LastCudaFailure 仅保留诊断信息，不参与后端决策。
        if (settings.ForceCpuForNextLaunch)
        {
            _logger.LogWarning(
                "Bootstrap selecting CPU because ForceCpuForNextLaunch is set (one-shot protection). " +
                "LastCudaFailure={LastCudaFailure}, RuntimeVersion={RuntimeVersion}. Consuming the flag.",
                settings.LastCudaFailure,
                _manifestProvider.PaddleInferenceVersion);
            settings.ForceCpuForNextLaunch = false;
            _ = _settingsHost.SaveConfigAsync();
            LoadCpuAndCommit(
                devices: null,
                selectedDevice: null,
                cudaInstalled: false,
                cudaCompatible: false,
                error: !string.IsNullOrEmpty(settings.LastCudaFailure)
                    ? $"Previous CUDA failure: {settings.LastCudaFailure}"
                    : "Previous CUDA failure (one-shot CPU fallback).");
            return;
        }

        // 3. 版本升级后清除过期的 CUDA 故障诊断信息（仅清理，不短路）。
        if (!string.IsNullOrEmpty(settings.LastCudaFailure)
            && !string.Equals(
                settings.LastCudaFailureRuntimeVersion,
                _manifestProvider.PaddleInferenceVersion,
                StringComparison.Ordinal))
        {
            _logger.LogInformation(
                "LastCudaFailure was recorded for runtime version {LastVersion} but current version is {CurrentVersion}. Clearing stale failure record.",
                settings.LastCudaFailureRuntimeVersion,
                _manifestProvider.PaddleInferenceVersion);
            settings.LastCudaFailure = null;
            settings.LastCudaFailureRuntimeVersion = null;
        }

        // 4. 进程架构检查：PaddleInference native runtime 仅支持 x64
        if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            _logger.LogWarning(
                "Bootstrap selecting CPU because process architecture is not X64. ProcessArchitecture={ProcessArchitecture}",
                RuntimeInformation.ProcessArchitecture);
            LoadCpuAndCommit(
                devices: null,
                selectedDevice: null,
                cudaInstalled: false,
                cudaCompatible: false,
                error: null);
            return;
        }

        // 5. 用户偏好 CPU
        if (preferredBackend == OcrInferenceBackend.Cpu)
        {
            _logger.LogInformation("Bootstrap selecting CPU because PreferredOcrBackend is Cpu.");
            LoadCpuAndCommit(
                devices: null,
                selectedDevice: null,
                cudaInstalled: false,
                cudaCompatible: false,
                error: null);
            return;
        }

        // 6. 尝试 CUDA
        _logger.LogInformation(
            "Attempting CUDA runtime bootstrap. PreferredBackend={PreferredBackend}",
            preferredBackend);

        var devices = _cudaDetector.DetectDevices();
        _logger.LogInformation("CUDA device detection completed. DeviceCount={DeviceCount}", devices.Count);

        // 选择设备：PreferredCudaDeviceId 优先（须存在且受支持），否则第一个 IsSupported
        CudaDeviceInfo? selectedDevice = null;
        if (settings.PreferredCudaDeviceId.HasValue)
        {
            var preferred = devices.FirstOrDefault(d => d.DeviceId == settings.PreferredCudaDeviceId.Value);
            if (preferred is { IsSupported: true })
            {
                selectedDevice = preferred;
            }
            else
            {
                _logger.LogWarning(
                    "Preferred CUDA device {PreferredDeviceId} is not available or not supported. PreferredFound={PreferredFound}, PreferredSupported={PreferredSupported}",
                    settings.PreferredCudaDeviceId.Value,
                    preferred != null,
                    preferred?.IsSupported ?? false);
            }
        }

        selectedDevice ??= devices.FirstOrDefault(d => d.IsSupported);

        if (selectedDevice == null)
        {
            _logger.LogWarning(
                "No supported CUDA device found. Falling back to CPU. DetectedDeviceCount={DeviceCount}",
                devices.Count);
            LoadCpuAndCommit(
                devices: devices,
                selectedDevice: null,
                cudaInstalled: false,
                cudaCompatible: false,
                error: "No supported CUDA device detected.");
            return;
        }

        _logger.LogInformation(
            "Selected CUDA device. DeviceId={DeviceId}, Name={DeviceName}, ComputeCapability={Major}.{Minor}",
            selectedDevice.DeviceId,
            selectedDevice.DeviceName,
            selectedDevice.ComputeCapabilityMajor,
            selectedDevice.ComputeCapabilityMinor);

        // 检查 CUDA runtime 组件安装状态
        var installInfo = _componentService.GetInstallStatus();
        var cudaInstalled = installInfo.Status != PaddleRuntimeInstallStatus.NotInstalled;
        var cudaCompatible = installInfo.Status == PaddleRuntimeInstallStatus.Installed;

        if (installInfo.Status != PaddleRuntimeInstallStatus.Installed)
        {
            _logger.LogWarning(
                "CUDA runtime component is not ready. Status={Status}, PackageId={PackageId}. Falling back to CPU.",
                installInfo.Status,
                installInfo.PackageId);
            LoadCpuAndCommit(
                devices: devices,
                selectedDevice: selectedDevice,
                cudaInstalled: cudaInstalled,
                cudaCompatible: cudaCompatible,
                error: $"CUDA runtime component status is {installInfo.Status}.");
            return;
        }

        if (_prerequisiteSetupService.Status.Status != PaddleCudaPrerequisiteInstallStatus.Installed)
        {
            _logger.LogWarning(
                "CUDA/cuDNN prerequisites are not ready. Status={Status}. Falling back to CPU.",
                _prerequisiteSetupService.Status.Status);
            LoadCpuAndCommit(
                devices: devices,
                selectedDevice: selectedDevice,
                cudaInstalled: cudaInstalled,
                cudaCompatible: cudaCompatible,
                error: "CUDA/cuDNN prerequisites are not installed.");
            return;
        }

        // 确认 manifest 中存在匹配 Compute Capability 的包
        var package = _manifestProvider.ResolveByComputeCapability(
            selectedDevice.ComputeCapabilityMajor,
            selectedDevice.ComputeCapabilityMinor);
        if (package == null)
        {
            _logger.LogWarning(
                "No manifest package matches Compute Capability {Major}.{Minor}. Falling back to CPU.",
                selectedDevice.ComputeCapabilityMajor,
                selectedDevice.ComputeCapabilityMinor);
            LoadCpuAndCommit(
                devices: devices,
                selectedDevice: selectedDevice,
                cudaInstalled: cudaInstalled,
                cudaCompatible: cudaCompatible,
                error: $"No manifest package for Compute Capability {selectedDevice.ComputeCapabilityMajor}.{selectedDevice.ComputeCapabilityMinor}.");
            return;
        }

        // CUDA runtime native 目录来自 install.json
        var cudaRuntimeDirectory = installInfo.NativeDirectory;
        if (string.IsNullOrWhiteSpace(cudaRuntimeDirectory) || !Directory.Exists(cudaRuntimeDirectory))
        {
            _logger.LogWarning(
                "CUDA runtime NativeDirectory is missing or does not exist. NativeDirectory={NativeDirectory}. Falling back to CPU.",
                cudaRuntimeDirectory);
            LoadCpuAndCommit(
                devices: devices,
                selectedDevice: selectedDevice,
                cudaInstalled: cudaInstalled,
                cudaCompatible: cudaCompatible,
                error: "CUDA runtime native directory is missing.");
            return;
        }

        _logger.LogInformation(
            "Loading CUDA Paddle runtime. DeviceId={DeviceId}, PackageId={PackageId}, NativeDirectory={NativeDirectory}",
            selectedDevice.DeviceId,
            package.PackageId,
            cudaRuntimeDirectory);

        RegisterPrerequisiteSearchDirectories();

        // 尝试加载 CUDA DLL
        if (TryLoadNativeRuntime(cudaRuntimeDirectory, out var cudaModulePath))
        {
            _logger.LogInformation(
                "CUDA Paddle runtime native DLL loaded. ModulePath={ModulePath}", cudaModulePath);

            // 真实 GPU Predictor probe：LoadLibrary 成功 ≠ Predictor 能在 GPU 上跑。
            // runtime 与驱动/cuDNN 不匹配、显存初始化失败等只在实际创建 Predictor 时才暴露。
            // probe 失败则回退 CPU，避免 UI 显示 CUDA 启用但实际推理走 CPU 的假状态。
            if (!PaddleRuntimeProbe.TryProbeCudaPredictor(selectedDevice.DeviceId, _logger))
            {
                _logger.LogWarning(
                    "CUDA Predictor probe failed. Falling back to CPU. DeviceId={DeviceId}",
                    selectedDevice.DeviceId);
                LoadCpuAndCommit(
                    devices: devices,
                    selectedDevice: selectedDevice,
                    cudaInstalled: cudaInstalled,
                    cudaCompatible: cudaCompatible,
                    error: "CUDA Predictor probe failed. Runtime may not match the installed driver or cuDNN.");
                return;
            }

            _logger.LogInformation(
                "CUDA Paddle runtime ready (probe passed). ModulePath={ModulePath}", cudaModulePath);
            _state.SetDetectedDevices(devices, selectedDevice);
            _state.SetCudaRuntimeStatus(cudaInstalled, cudaCompatible);
            _state.SetActiveBackend(OcrInferenceBackend.Cuda, selectedDevice.DeviceId, cudaModulePath);
            _state.SetRuntimeLoadError(null);
            return;
        }

        // CUDA 加载失败，回退 CPU
        _logger.LogWarning(
            "CUDA Paddle runtime load failed. Falling back to CPU. NativeDirectory={NativeDirectory}",
            cudaRuntimeDirectory);
        LoadCpuAndCommit(
            devices: devices,
            selectedDevice: selectedDevice,
            cudaInstalled: cudaInstalled,
            cudaCompatible: cudaCompatible,
            error: "GPU runtime load failed.");
    }

    /// <summary>
    /// 加载 CPU runtime 并把最终状态写入 <see cref="IPaddleRuntimeState"/>。
    /// CPU runtime 目录为 <c>{ModuleRoot}/Runtime/Paddle/cpu/{PaddleInferenceRuntimeVersion}/native</c>。
    /// </summary>
    /// <param name="devices">已检测到的 CUDA 设备列表；尚未检测时为 <see langword="null"/>。</param>
    /// <param name="selectedDevice">已选中的 CUDA 设备；无设备时为 <see langword="null"/>。</param>
    /// <param name="cudaInstalled">CUDA runtime 组件是否已安装（用于状态展示）。</param>
    /// <param name="cudaCompatible">CUDA runtime 组件是否与当前版本兼容（用于状态展示）。</param>
    /// <param name="error">回退原因；正常选择 CPU 时为 <see langword="null"/>。</param>
    private void LoadCpuAndCommit(
        IReadOnlyList<CudaDeviceInfo>? devices,
        CudaDeviceInfo? selectedDevice,
        bool cudaInstalled,
        bool cudaCompatible,
        string? error)
    {
        var cpuRuntimeDirectory = Path.Combine(
            _moduleStorage.PaddleRuntimeRoot,
            "cpu",
            _manifestProvider.PaddleInferenceVersion,
            "native");

        string? modulePath = null;
        if (TryLoadNativeRuntime(cpuRuntimeDirectory, out var loadedPath))
        {
            modulePath = loadedPath;
            _logger.LogInformation("CPU Paddle runtime loaded. ModulePath={ModulePath}", modulePath);
        }
        else
        {
            _logger.LogError(
                "CPU Paddle runtime load failed. Directory={Directory}",
                cpuRuntimeDirectory);
            error ??= "CPU runtime load failed.";
        }

        _state.SetDetectedDevices(devices ?? Array.Empty<CudaDeviceInfo>(), selectedDevice);
        _state.SetCudaRuntimeStatus(cudaInstalled, cudaCompatible);
        _state.SetActiveBackend(OcrInferenceBackend.Cpu, -1, modulePath);
        _state.SetRuntimeLoadError(error);
    }

    /// <summary>
    /// 在指定 runtime 目录中加载 <c>paddle_inference_c.dll</c>，并验证实际加载模块路径位于该目录内。
    /// </summary>
    /// <param name="runtimeDirectory">runtime native 目录绝对路径。</param>
    /// <param name="modulePath">加载成功时输出实际模块完整路径；失败时为 <see langword="null"/>。</param>
    /// <returns>加载并验证成功返回 <see langword="true"/>；否则 <see langword="false"/>。</returns>
    private bool TryLoadNativeRuntime(string runtimeDirectory, [NotNullWhen(true)] out string? modulePath)
    {
        modulePath = null;

        if (string.IsNullOrWhiteSpace(runtimeDirectory) || !Directory.Exists(runtimeDirectory))
        {
            _logger.LogWarning("Runtime directory does not exist. Directory={Directory}", runtimeDirectory);
            return false;
        }

        var dllPath = Path.Combine(runtimeDirectory, NativeModuleName);
        if (!File.Exists(dllPath))
        {
            _logger.LogWarning("Paddle native module does not exist. Path={Path}", dllPath);
            return false;
        }

        // 配置进程默认 DLL 搜索标志：用户目录 + system32，不搜索当前目录
        if (!SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_USER_DIRS | LOAD_LIBRARY_SEARCH_SYSTEM32))
        {
            var win32Error = Marshal.GetLastWin32Error();
            _logger.LogWarning(
                "SetDefaultDllDirectories failed. Win32Error={Win32Error}",
                win32Error);
        }

        // 注册 runtime 目录为用户 DLL 搜索目录
        var cookie = AddDllDirectory(runtimeDirectory);
        if (cookie == IntPtr.Zero)
        {
            var win32Error = Marshal.GetLastWin32Error();
            _logger.LogWarning(
                "AddDllDirectory failed. Directory={Directory}, Win32Error={Win32Error}",
                runtimeDirectory,
                win32Error);
        }

        // 加载 paddle_inference_c.dll
        var flags = LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_USER_DIRS | LOAD_LIBRARY_SEARCH_SYSTEM32;
        var handle = LoadLibraryExW(dllPath, IntPtr.Zero, flags);
        if (handle == IntPtr.Zero)
        {
            var win32Error = Marshal.GetLastWin32Error();
            _logger.LogError(
                "LoadLibraryExW failed. Path={Path}, Win32Error={Win32Error}",
                dllPath,
                win32Error);
            return false;
        }

        // 验证实际加载的模块路径，确保 Windows 没有加载到错误版本
        if (!TryVerifyLoadedModulePath(runtimeDirectory, out var actualPath))
        {
            _logger.LogError(
                "Loaded paddle_inference_c.dll is not located in the selected runtime directory. SelectedDirectory={Directory}, ActualPath={ActualPath}",
                runtimeDirectory,
                actualPath);
            FreeLibrary(handle);
            return false;
        }

        _logger.LogInformation(
            "Verified loaded Paddle native module. ActualPath={ActualPath}",
            actualPath);
        modulePath = actualPath;
        return true;
    }

    /// <summary>
    /// 将模块管理的 CUDA/cuDNN 目录加入当前进程的安全 DLL 搜索路径。
    /// 该目录仅在已验证的安装 manifest 存在时由依赖服务返回。
    /// </summary>
    private void RegisterPrerequisiteSearchDirectories()
    {
        foreach (var directory in _prerequisiteSetupService.GetDllSearchDirectories())
        {
            if (!Directory.Exists(directory))
                continue;

            var cookie = AddDllDirectory(directory);
            if (cookie == IntPtr.Zero)
            {
                _logger.LogWarning(
                    "AddDllDirectory failed for CUDA prerequisite directory. Directory={Directory}, Win32Error={Win32Error}",
                    directory,
                    Marshal.GetLastWin32Error());
            }
            else
            {
                _logger.LogInformation("Registered CUDA prerequisite DLL directory. Directory={Directory}", directory);
            }
        }
    }

    /// <summary>
    /// 在当前进程已加载模块中查找 <c>paddle_inference_c.dll</c>，并验证其路径位于指定目录内。
    /// </summary>
    /// <param name="expectedDirectory">期望的 runtime 目录绝对路径。</param>
    /// <param name="actualPath">输出实际模块路径；未找到时为 <see langword="null"/>。</param>
    /// <returns>模块已加载且路径位于期望目录内返回 <see langword="true"/>；否则 <see langword="false"/>。</returns>
    private bool TryVerifyLoadedModulePath(string expectedDirectory, out string? actualPath)
    {
        actualPath = null;
        try
        {
            var module = Process.GetCurrentProcess().Modules.OfType<ProcessModule>()
                .FirstOrDefault(m => string.Equals(m.ModuleName, NativeModuleName, StringComparison.OrdinalIgnoreCase));
            if (module == null)
            {
                _logger.LogWarning("paddle_inference_c.dll was not found in loaded process modules.");
                return false;
            }

            actualPath = module.FileName;
            if (string.IsNullOrWhiteSpace(actualPath))
            {
                return false;
            }

            var normalizedExpected = Path.GetFullPath(expectedDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var normalizedActual = Path.GetFullPath(actualPath);
            if (!normalizedActual.StartsWith(normalizedExpected, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to verify loaded module path. ExpectedDirectory={Directory}",
                expectedDirectory);
            return false;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibraryExW(string lpLibFileName, IntPtr hFile, uint dwFlags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetDefaultDllDirectories(uint directoryFlags);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr AddDllDirectory(string lpPathName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeLibrary(IntPtr hModule);
}
