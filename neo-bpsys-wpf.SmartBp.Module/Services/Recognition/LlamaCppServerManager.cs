using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

namespace neo_bpsys_wpf.SmartBp.Module.Services.Recognition;

/// <summary>
/// 管理单个 llama.cpp 视觉识别服务进程的启动、健康检查、状态记录和停止。
/// </summary>
internal sealed class LlamaCppServerManager : ILlamaCppServerManager, IDisposable
{
    private readonly IQwenModelAssetManager _assets;
    private readonly ISmartBpRecognitionSettingsService _settings;
    private readonly ISmartBpModuleStorageProvider _storage;
    private readonly ILogger<LlamaCppServerManager> _logger;
    private readonly ISmartBpDebugLog _debugLog;
    private readonly ILlamaCppRuntimeAssetManager _runtimeAssets;
    private readonly LlamaVisionServerRole _role;
    private Process? _process;
    private string StateFilePath => Path.Combine(AppConstants.AppDataPath, "SmartBp", $"LlamaServerProcess.{_role}.json");
    /// <inheritdoc />
    public LlamaVisionServerRole Role => _role;
    /// <inheritdoc />
    public int Port => GetPort();
    /// <inheritdoc />
    public bool IsRunning => _process is { HasExited: false };
    /// <inheritdoc />
    public string Status { get; private set; } = "Stopped";
    /// <inheritdoc />
    public int? ProcessId => IsRunning ? _process?.Id : null;

    /// <summary>
    /// 初始化业务 AI 使用的 llama.cpp 服务进程管理器。
    /// </summary>
    /// <param name="assets">本地视觉模型资产管理器。</param>
    /// <param name="settings">SmartBP 识别设置服务。</param>
    /// <param name="storage">SmartBP 模块存储提供程序。</param>
    /// <param name="logger">日志记录器。</param>
    /// <param name="debugLog">SmartBP 识别调试日志。</param>
    /// <param name="runtimeAssets">llama.cpp 运行时资产管理器。</param>
    public LlamaCppServerManager(IQwenModelAssetManager assets, ISmartBpRecognitionSettingsService settings,
        ISmartBpModuleStorageProvider storage, ILogger<LlamaCppServerManager> logger, ISmartBpDebugLog debugLog,
        ILlamaCppRuntimeAssetManager runtimeAssets)
        : this(assets, settings, storage, logger, debugLog, runtimeAssets, LlamaVisionServerRole.BusinessAi)
    {
    }

    /// <summary>
    /// 初始化指定角色的 llama.cpp 服务进程管理器。
    /// </summary>
    /// <param name="assets">本地视觉模型资产管理器。</param>
    /// <param name="settings">SmartBP 识别设置服务。</param>
    /// <param name="storage">SmartBP 模块存储提供程序。</param>
    /// <param name="logger">日志记录器。</param>
    /// <param name="debugLog">SmartBP 识别调试日志。</param>
    /// <param name="runtimeAssets">llama.cpp 运行时资产管理器。</param>
    /// <param name="role">服务角色。</param>
    public LlamaCppServerManager(IQwenModelAssetManager assets, ISmartBpRecognitionSettingsService settings,
        ISmartBpModuleStorageProvider storage, ILogger<LlamaCppServerManager> logger, ISmartBpDebugLog debugLog,
        ILlamaCppRuntimeAssetManager runtimeAssets, LlamaVisionServerRole role)
    {
        _assets = assets; _settings = settings; _storage = storage; _logger = logger; _debugLog = debugLog; _runtimeAssets = runtimeAssets;
        _role = role;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning) throw new InvalidOperationException("llama-server is already running.");
        var executable = _settings.Settings.LlamaServerExecutablePath;
        if (string.IsNullOrWhiteSpace(executable)) executable = await _runtimeAssets.GetInstalledExecutablePathAsync(cancellationToken);
        if (!File.Exists(executable)) throw new FileNotFoundException("llama-server.exe was not found.", executable);
        var port = GetPort();
        if (await IsPortOccupiedAsync(port, cancellationToken))
            await RecoverManagedPortConflictAsync(executable, cancellationToken);
        var modelId = GetModelId();
        var installed = await _assets.GetInstalledPathsAsync(modelId, cancellationToken);
        var profile = await _assets.GetProfileAsync(modelId, cancellationToken);
        if (installed.MmprojMode == QwenMmprojMode.None)
            throw new InvalidOperationException("The selected local vision profile has no vision projector. SmartBP image recognition is unavailable for this profile.");
        var model = installed.ModelPath;
        Directory.CreateDirectory(_storage.RecognitionLogsRoot); var log = Path.Combine(_storage.RecognitionLogsRoot, $"llama-server-{_role}-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        var info = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        var gpuLayers = _settings.Settings.LlamaGpuLayers < 0 ? "auto" : _settings.Settings.LlamaGpuLayers.ToString();
        var flashAttention = _settings.Settings.LlamaFlashAttention ? "auto" : "off";
        var arguments = new List<string> { "--model", model };
        if (installed.MmprojMode == QwenMmprojMode.Separate)
        {
            if (string.IsNullOrWhiteSpace(installed.MmprojPath))
                throw new FileNotFoundException("The selected Qwen profile requires a separate vision projector.");
            arguments.AddRange(["--mmproj", installed.MmprojPath]);
        }
        arguments.AddRange(["--host", "127.0.0.1", "--port", port.ToString(), "--ctx-size", _settings.Settings.LlamaContextSize.ToString(), "--n-gpu-layers", gpuLayers, "--flash-attn", flashAttention, "--parallel", _settings.Settings.LlamaParallelSlots.ToString(), "--batch-size", _settings.Settings.LlamaBatchSize.ToString(), "--ubatch-size", _settings.Settings.LlamaUBatchSize.ToString(), "--no-webui", "--log-file", log, "--threads", _settings.Settings.CpuThreads.ToString()]);
        foreach (var arg in arguments) info.ArgumentList.Add(arg);
        _logger.LogInformation("Starting llama-server. Role={Role}, Port={Port}, Model={Model}, Log={Log}", _role, port, Path.GetFileName(model), log);
        _debugLog.Write("llama-server", $"Role={_role}; local vision profile: {profile.Id} ({profile.DisplayName}); mmproj mode: {installed.MmprojMode}; model path: {model}; mmproj path: {installed.MmprojPath ?? "not used"}");
        _debugLog.Write("llama-server", $"Command: {Quote(executable)} {string.Join(" ", arguments.Select(Quote))}");
        _debugLog.Write("llama-server", $"Log file: {log}");
        _process = await Task.Run(() => StartProcess(info), cancellationToken).ConfigureAwait(false);
        await WriteStateAsync(_process.Id, port, executable, model, cancellationToken).ConfigureAwait(false);
        Status = "Starting";
        try
        {
            using var http = new HttpClient(); var deadline = DateTime.UtcNow.AddSeconds(_settings.Settings.AiStartupTimeoutSeconds);
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested(); if (_process.HasExited) throw new InvalidOperationException($"llama-server exited with code {_process.ExitCode}.");
                try { using var response = await http.GetAsync($"http://127.0.0.1:{port}/health", cancellationToken); if (response.IsSuccessStatusCode) { Status = "Ready"; _logger.LogInformation("llama-server responsive"); _debugLog.Write("health", $"llama-server {_role} is ready."); return; } } catch (HttpRequestException) { }
                await Task.Delay(500, cancellationToken);
            }
            throw new TimeoutException($"llama-server did not become responsive within {_settings.Settings.AiStartupTimeoutSeconds} seconds.");
        }
        catch (Exception ex) { _debugLog.Write("health", $"Server startup failed: {ex.Message}"); await StopAsync(); _logger.LogWarning("llama-server not responsive"); throw; }
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        var process = _process;
        _process = null;
        if (process is { HasExited: false })
        {
            _debugLog.Write("llama-server", "Stopping managed process...");
            await Task.Run(() => { process.Kill(true); process.WaitForExit(5000); }).ConfigureAwait(false);
        }
        process?.Dispose(); Status = "Stopped";
        DeleteStateFile();
    }

    /// <inheritdoc />
    public async Task ForceStopManagedProcessAsync(CancellationToken cancellationToken = default)
    {
        var state = await ReadStateAsync(cancellationToken).ConfigureAwait(false);
        if (state == null) throw new InvalidOperationException("No managed llama-server process state is recorded.");
        await KillRecordedProcessAsync(state, cancellationToken).ConfigureAwait(false);
        DeleteStateFile();
        if (_process != null)
        {
            _process.Dispose();
            _process = null;
        }
        Status = "Stopped";
        _debugLog.Write("llama-server", $"Force-stopped recorded managed process pid={state.Pid}.");
    }
    private Process StartProcess(ProcessStartInfo info)
    {
        var process = Process.Start(info) ?? throw new InvalidOperationException("Failed to start llama-server.");
        process.OutputDataReceived += OnOutputDataReceived;
        process.ErrorDataReceived += OnErrorDataReceived;
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        try { process.PriorityClass = Enum.TryParse<ProcessPriorityClass>(_settings.Settings.ProcessPriority, true, out var priority) ? priority : ProcessPriorityClass.BelowNormal; }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not set llama-server priority"); }
        return process;
    }
    private void OnOutputDataReceived(object sender, DataReceivedEventArgs e) { if (!string.IsNullOrWhiteSpace(e.Data)) _debugLog.Write("llama-server", e.Data); }
    private void OnErrorDataReceived(object sender, DataReceivedEventArgs e) { if (!string.IsNullOrWhiteSpace(e.Data)) _debugLog.Write("llama-server stderr", e.Data); }
    private static string Quote(string value) => value.Any(char.IsWhiteSpace) ? $"\"{value.Replace("\"", "\\\"")}\"" : value;
    private static async Task<bool> IsPortOccupiedAsync(int port, CancellationToken token) { using var client = new TcpClient(); try { await client.ConnectAsync("127.0.0.1", port, token); return true; } catch (SocketException) { return false; } }
    private async Task RecoverManagedPortConflictAsync(string executable, CancellationToken cancellationToken)
    {
        var state = await ReadStateAsync(cancellationToken).ConfigureAwait(false);
        var port = GetPort();
        if (state == null) throw new InvalidOperationException($"Port {port} is already occupied by an unknown process.");
        if (state.Port != port || !SamePath(state.ExecutablePath, executable))
            throw new InvalidOperationException($"Port {port} is occupied, but the recorded managed llama-server state does not match the selected executable.");
        if (!_settings.Settings.AutoKillStaleManagedLlamaServer)
            throw new InvalidOperationException($"Port {port} is occupied by a recorded managed llama-server process. Use Force stop llama.cpp or enable stale-process cleanup.");
        await KillRecordedProcessAsync(state, cancellationToken).ConfigureAwait(false);
        DeleteStateFile();
        await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        if (await IsPortOccupiedAsync(port, cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException($"Port {port} is still occupied after stopping the recorded managed process.");
    }

    private static bool SamePath(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    private async Task<LlamaServerProcessState?> ReadStateAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(StateFilePath)) return null;
        await using var stream = File.OpenRead(StateFilePath);
        return await JsonSerializer.DeserializeAsync<LlamaServerProcessState>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteStateAsync(int pid, int port, string executable, string model, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StateFilePath)!);
        var state = new LlamaServerProcessState(pid, port, executable, model, DateTimeOffset.Now);
        await File.WriteAllTextAsync(StateFilePath, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }), cancellationToken).ConfigureAwait(false);
    }

    private async Task KillRecordedProcessAsync(LlamaServerProcessState state, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Process? process = null;
        try { process = Process.GetProcessById(state.Pid); }
        catch (ArgumentException) { return; }
        await Task.Run(() =>
        {
            try { process.Kill(true); process.WaitForExit(5000); }
            finally { process.Dispose(); }
        }, cancellationToken).ConfigureAwait(false);
    }

    private void DeleteStateFile()
    {
        try { if (File.Exists(StateFilePath)) File.Delete(StateFilePath); }
        catch { }
    }
    private void OnProcessExit(object? sender, EventArgs e)
    {
        try { if (_process is { HasExited: false }) _process.Kill(true); }
        catch { }
        DeleteStateFile();
    }
    /// <inheritdoc />
    public void Dispose() { AppDomain.CurrentDomain.ProcessExit -= OnProcessExit; OnProcessExit(this, EventArgs.Empty); _process?.Dispose(); _process = null; }

    private int GetPort() => _role == LlamaVisionServerRole.AiOcr
        ? _settings.Settings.AiOcrServerPort
        : _settings.Settings.BusinessAiServerPort;

    private string GetModelId() => _role == LlamaVisionServerRole.AiOcr
        ? _settings.Settings.SelectedAiOcrModelId
        : _settings.Settings.SelectedBusinessAiModelId;

    private sealed record LlamaServerProcessState(int Pid, int Port, string ExecutablePath, string ModelPath, DateTimeOffset StartedAt);
}

/// <summary>
/// 按识别角色分发业务 AI 与 AI OCR 两个 llama.cpp 服务进程管理器。
/// </summary>
internal sealed class LlamaCppServerManagerFactory : ILlamaCppServerManagerFactory, IDisposable
{
    private readonly ILlamaCppServerManager _business;
    private readonly LlamaCppServerManager _aiOcr;

    /// <summary>
    /// 初始化 llama.cpp 服务进程管理器工厂。
    /// </summary>
    /// <param name="assets">本地视觉模型资产管理器。</param>
    /// <param name="settings">SmartBP 识别设置服务。</param>
    /// <param name="storage">SmartBP 模块存储提供程序。</param>
    /// <param name="logger">日志记录器。</param>
    /// <param name="debugLog">SmartBP 识别调试日志。</param>
    /// <param name="runtimeAssets">llama.cpp 运行时资产管理器。</param>
    /// <param name="business">业务 AI 服务进程管理器。</param>
    public LlamaCppServerManagerFactory(
        IQwenModelAssetManager assets,
        ISmartBpRecognitionSettingsService settings,
        ISmartBpModuleStorageProvider storage,
        ILogger<LlamaCppServerManager> logger,
        ISmartBpDebugLog debugLog,
        ILlamaCppRuntimeAssetManager runtimeAssets,
        ILlamaCppServerManager business)
    {
        _business = business;
        _aiOcr = new LlamaCppServerManager(assets, settings, storage, logger, debugLog, runtimeAssets, LlamaVisionServerRole.AiOcr);
    }

    /// <inheritdoc />
    public ILlamaCppServerManager Get(LlamaVisionServerRole role) =>
        role == LlamaVisionServerRole.AiOcr ? _aiOcr : _business;

    /// <inheritdoc />
    public void Dispose() => _aiOcr.Dispose();
}
