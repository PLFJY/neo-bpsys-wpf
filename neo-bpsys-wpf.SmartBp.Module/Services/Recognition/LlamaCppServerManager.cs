using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;

namespace neo_bpsys_wpf.SmartBp.Module.Services.Recognition;

internal sealed class LlamaCppServerManager : ILlamaCppServerManager, IDisposable
{
    private readonly IQwenModelAssetManager _assets;
    private readonly ISmartBpRecognitionSettingsService _settings;
    private readonly ISmartBpModuleStorageProvider _storage;
    private readonly ILogger<LlamaCppServerManager> _logger;
    private readonly ISmartBpDebugLog _debugLog;
    private readonly ILlamaCppRuntimeAssetManager _runtimeAssets;
    private Process? _process;
    public bool IsRunning => _process is { HasExited: false };
    public string Status { get; private set; } = "Stopped";

    public LlamaCppServerManager(IQwenModelAssetManager assets, ISmartBpRecognitionSettingsService settings,
        ISmartBpModuleStorageProvider storage, ILogger<LlamaCppServerManager> logger, ISmartBpDebugLog debugLog,
        ILlamaCppRuntimeAssetManager runtimeAssets)
    {
        _assets = assets; _settings = settings; _storage = storage; _logger = logger; _debugLog = debugLog; _runtimeAssets = runtimeAssets;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning) throw new InvalidOperationException("llama-server is already running.");
        var executable = _settings.Settings.LlamaServerExecutablePath;
        if (string.IsNullOrWhiteSpace(executable)) executable = await _runtimeAssets.GetInstalledExecutablePathAsync(cancellationToken);
        if (!File.Exists(executable)) throw new FileNotFoundException("llama-server.exe was not found.", executable);
        if (await IsPortOccupiedAsync(_settings.Settings.LlamaServerPort, cancellationToken)) throw new InvalidOperationException($"Port {_settings.Settings.LlamaServerPort} is already occupied.");
        var (model, mmproj) = await _assets.GetInstalledPathsAsync(cancellationToken);
        Directory.CreateDirectory(_storage.RecognitionLogsRoot); var log = Path.Combine(_storage.RecognitionLogsRoot, $"llama-server-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        var info = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        var arguments = new[] { "--model", model, "--mmproj", mmproj, "--host", "127.0.0.1", "--port", _settings.Settings.LlamaServerPort.ToString(), "--ctx-size", _settings.Settings.LlamaContextSize.ToString(), "--n-gpu-layers", "auto", "--flash-attn", "auto", "--parallel", "1", "--no-webui", "--log-file", log, "--threads", _settings.Settings.CpuThreads.ToString() };
        foreach (var arg in arguments) info.ArgumentList.Add(arg);
        _logger.LogInformation("Starting llama-server. Port={Port}, Model={Model}, Log={Log}", _settings.Settings.LlamaServerPort, Path.GetFileName(model), log);
        _debugLog.Write("llama-server", $"Command: {Quote(executable)} {string.Join(" ", arguments.Select(Quote))}");
        _debugLog.Write("llama-server", $"Log file: {log}");
        _process = await Task.Run(() => StartProcess(info), cancellationToken).ConfigureAwait(false);
        Status = "Starting";
        try
        {
            using var http = new HttpClient(); var deadline = DateTime.UtcNow.AddMinutes(2);
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested(); if (_process.HasExited) throw new InvalidOperationException($"llama-server exited with code {_process.ExitCode}.");
                try { using var response = await http.GetAsync($"http://127.0.0.1:{_settings.Settings.LlamaServerPort}/health", cancellationToken); if (response.IsSuccessStatusCode) { Status = "Ready"; _logger.LogInformation("llama-server responsive"); _debugLog.Write("health", "llama-server is ready."); return; } } catch (HttpRequestException) { }
                await Task.Delay(500, cancellationToken);
            }
            throw new TimeoutException("llama-server did not become responsive within two minutes.");
        }
        catch (Exception ex) { _debugLog.Write("health", $"Server startup failed: {ex.Message}"); await StopAsync(); _logger.LogWarning("llama-server not responsive"); throw; }
    }

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
    private void OnProcessExit(object? sender, EventArgs e)
    {
        try { if (_process is { HasExited: false }) _process.Kill(true); }
        catch { }
    }
    public void Dispose() { AppDomain.CurrentDomain.ProcessExit -= OnProcessExit; OnProcessExit(this, EventArgs.Empty); _process?.Dispose(); _process = null; }
}
