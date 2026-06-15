using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Messages;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Messages;
using neo_bpsys_wpf.Services.Abstractions;
using neo_bpsys_wpf.Views.Pages;
using neo_bpsys_wpf.Views.Windows;
using System.IO;
using System.IO.Pipes;
using System.Windows;
using Wpf.Ui;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// Imports and activates <c>.bpui</c> files opened through Windows file association.
/// </summary>
public sealed class BpuiFileActivationService : IBpuiFileActivationService
{
    private const string PipeName = AppConstants.AppName + ".bpui-open";

    private readonly IFrontedLayoutPackageImporter _packageImporter;
    private readonly IFrontedLayoutPackageLegacyConverter _legacyPackageConverter;
    private readonly IFrontedWindowService _frontedWindowService;
    private readonly IFrontedBehaviorRuntime? _behaviorRuntime;
    private readonly INavigationService _navigationService;
    private readonly IInfoBarService _infoBarService;
    private readonly ILogger<BpuiFileActivationService> _logger;
    private readonly SemaphoreSlim _importLock = new(1, 1);
    private CancellationTokenSource? _listenCancellation;
    private Task? _listenTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="BpuiFileActivationService"/> class.
    /// </summary>
    /// <param name="packageImporter">Layout package importer.</param>
    /// <param name="legacyPackageConverter">Legacy package converter.</param>
    /// <param name="frontedWindowService">Fronted window service.</param>
    /// <param name="behaviorRuntime">Behavior runtime.</param>
    /// <param name="navigationService">Navigation service.</param>
    /// <param name="infoBarService">InfoBar service.</param>
    /// <param name="logger">Logger.</param>
    public BpuiFileActivationService(
        IFrontedLayoutPackageImporter packageImporter,
        IFrontedLayoutPackageLegacyConverter legacyPackageConverter,
        IFrontedWindowService frontedWindowService,
        IFrontedBehaviorRuntime? behaviorRuntime,
        INavigationService navigationService,
        IInfoBarService infoBarService,
        ILogger<BpuiFileActivationService> logger)
    {
        _packageImporter = packageImporter;
        _legacyPackageConverter = legacyPackageConverter;
        _frontedWindowService = frontedWindowService;
        _behaviorRuntime = behaviorRuntime;
        _navigationService = navigationService;
        _infoBarService = infoBarService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public void StartListening(CancellationToken cancellationToken = default)
    {
        if (_listenTask is { IsCompleted: false })
        {
            return;
        }

        _listenCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listenTask = Task.Run(() => ListenAsync(_listenCancellation.Token), CancellationToken.None);
    }

    /// <inheritdoc/>
    public void StopListening()
    {
        _listenCancellation?.Cancel();
        _listenCancellation?.Dispose();
        _listenCancellation = null;
        _listenTask = null;
    }

    /// <inheritdoc/>
    public async Task<bool> TryForwardToRunningInstanceAsync(
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            return false;
        }

        try
        {
            await using var pipe = new NamedPipeClientStream(
                ".",
                PipeName,
                PipeDirection.Out,
                PipeOptions.Asynchronous);
            await pipe.ConnectAsync(1500, cancellationToken);
            await using var writer = new StreamWriter(pipe) { AutoFlush = true };
            await writer.WriteLineAsync(packagePath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException)
        {
            _logger.LogDebug(ex, "Failed to forward bpui path to the running instance.");
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<BpuiFileActivationResult> OpenPackageAsync(
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        if (Application.Current.Dispatcher.CheckAccess())
        {
            return await OpenPackageOnDispatcherAsync(packagePath, cancellationToken);
        }

        return await Application.Current.Dispatcher.InvokeAsync(
            () => OpenPackageOnDispatcherAsync(packagePath, cancellationToken)).Task.Unwrap();
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                await pipe.WaitForConnectionAsync(cancellationToken);
                using var reader = new StreamReader(pipe);
                var packagePath = await reader.ReadLineAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(packagePath))
                {
                    _ = OpenPackageAsync(packagePath, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed while listening for bpui file activation.");
            }
        }
    }

    private async Task<BpuiFileActivationResult> OpenPackageOnDispatcherAsync(
        string packagePath,
        CancellationToken cancellationToken)
    {
        await _importLock.WaitAsync(cancellationToken);
        try
        {
            var normalizedPath = NormalizePackagePath(packagePath);
            if (normalizedPath is null)
            {
                return Fail("Package archive was not found.");
            }

            BringBackendWindowToFront();
            NavigateToLayoutPackageManager();
            var result = await ImportPackageAsync(normalizedPath, cancellationToken);
            if (!result.Success)
            {
                return Fail(result.ErrorMessage ?? I18nHelper.GetLocalizedString("PackageImportFailed"));
            }

            if (_behaviorRuntime is not null)
            {
                await _behaviorRuntime.StopAllLoopBehaviorsAsync(FrontedBehaviorStopReason.PackageSwitched);
            }

            await _frontedWindowService.ReloadFrontedLayoutsAsync();
            WeakReferenceMessenger.Default.Send(new FrontedLayoutPackagesChangedMessage(this, result.PackageId));
            NavigateToLayoutPackageManager();
            _infoBarService.ShowSuccessInfoBar(
                $"{I18nHelper.GetLocalizedString("PackageActivatedInstalled")}: {result.PackageId}");
            return new BpuiFileActivationResult(true, result.PackageId, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to open bpui package from file activation.");
            NavigateToLayoutPackageManager();
            return Fail(ex.Message);
        }
        finally
        {
            _importLock.Release();
        }
    }

    private async Task<FrontedLayoutPackageImportResult> ImportPackageAsync(
        string packagePath,
        CancellationToken cancellationToken)
    {
        var result = await _packageImporter.ImportAsync(new FrontedLayoutPackageImportRequest
        {
            PackagePath = packagePath,
            ReplaceExisting = true,
            ActivateAfterImport = true,
            PreserveMissingPlugins = true
        }, cancellationToken);
        if (!result.IsLegacyPackage)
        {
            return result;
        }

        var packageId = $"converted.legacy.{DateTimeOffset.Now:yyyyMMddHHmmss}.{Guid.NewGuid():N}"[..42];
        var packageName = Path.GetFileName(packagePath);
        var convertResult = await _legacyPackageConverter.ConvertAsync(new FrontedLayoutPackageLegacyConvertRequest
        {
            LegacyPackagePath = packagePath,
            PackageId = packageId,
            Name = string.IsNullOrWhiteSpace(packageName) ? packageId : packageName,
            Description = I18nHelper.GetLocalizedString("LegacyPackageDefaultDescription"),
            Author = string.Empty,
            MinVersion = string.Empty,
            InstallAfterConvert = false,
            ActivateAfterInstall = false
        }, cancellationToken);

        LogLegacyConversion(convertResult, packageId);
        if (!convertResult.Success || string.IsNullOrWhiteSpace(convertResult.ConvertedPackagePath))
        {
            return new FrontedLayoutPackageImportResult
            {
                Success = false,
                ErrorMessage = convertResult.ErrorMessage ?? I18nHelper.GetLocalizedString("LegacyPackageConvertFailed")
            };
        }

        return await _packageImporter.ImportAsync(new FrontedLayoutPackageImportRequest
        {
            PackagePath = convertResult.ConvertedPackagePath,
            ReplaceExisting = true,
            ActivateAfterImport = true,
            PreserveMissingPlugins = true
        }, cancellationToken);
    }

    private void LogLegacyConversion(FrontedLayoutPackageLegacyConvertResult convertResult, string packageId)
    {
        var details = LegacyConversionMessageFormatter.BuildTechnicalDetails(convertResult);
        if (!string.IsNullOrWhiteSpace(details))
        {
            _logger.LogInformation(
                "Legacy layout package conversion details for {PackageId}:{NewLine}{Details}",
                packageId,
                Environment.NewLine,
                details);
        }
    }

    private BpuiFileActivationResult Fail(string message)
    {
        NavigateToLayoutPackageManager();
        _infoBarService.ShowErrorInfoBar($"{I18nHelper.GetLocalizedString("PackageImportFailed")}: {message}");
        return new BpuiFileActivationResult(false, null, message);
    }

    private void NavigateToLayoutPackageManager()
    {
        try
        {
            _ = _navigationService.Navigate(typeof(FrontManagePage));
            WeakReferenceMessenger.Default.Send(
                new FrontManageTabNavigationMessage(FrontManageTabNavigationMessage.LayoutPackagesTabKey));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to navigate to the fronted layout package manager.");
        }
    }

    private static string? NormalizePackagePath(string packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            return null;
        }

        var trimmed = packagePath.Trim().Trim('"');
        if (!string.Equals(Path.GetExtension(trimmed), ".bpui", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(trimmed);
        return File.Exists(fullPath) ? fullPath : null;
    }

    private static void BringBackendWindowToFront()
    {
        var window = Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(item => item is MainWindow or ClassicBackendWindow);
        if (window is null)
        {
            return;
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Show();
        window.Activate();
    }
}
