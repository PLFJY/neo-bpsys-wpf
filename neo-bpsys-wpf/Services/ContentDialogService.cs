using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using System.Windows.Threading;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 内容对话框服务，负责将对话框显示在当前后台窗口的根宿主中。
/// </summary>
public sealed class ContentDialogService(ILogger<ContentDialogService> logger) : IContentDialogService
{
    private readonly ILogger<ContentDialogService> _logger = logger;
    private ContentDialogHost? _dialogHost;

    /// <inheritdoc />
    public void SetContentDialogHost(ContentDialogHost dialogHost)
    {
        ArgumentNullException.ThrowIfNull(dialogHost);
        _dialogHost = dialogHost;
    }

    /// <inheritdoc />
    public Task<ContentDialogResult> ShowAsync(ContentDialog dialog, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        var dialogHost = _dialogHost;
        if (dialogHost is null)
        {
            _logger.LogError("ContentDialogHost was never set before ShowAsync() was called.");
            throw new InvalidOperationException("The ContentDialogHost was never set.");
        }

        if (dialogHost.Dispatcher.CheckAccess())
        {
            return ShowCoreAsync(dialog, dialogHost, cancellationToken);
        }

        return dialogHost.Dispatcher
            .InvokeAsync(() => ShowCoreAsync(dialog, dialogHost, cancellationToken), DispatcherPriority.Normal)
            .Task
            .Unwrap();
    }

    private static Task<ContentDialogResult> ShowCoreAsync(
        ContentDialog dialog,
        ContentDialogHost dialogHost,
        CancellationToken cancellationToken)
    {
        if (dialog.DialogHostEx is not null && !ReferenceEquals(dialog.DialogHostEx, dialogHost))
        {
            throw new InvalidOperationException("The ContentDialog is already associated with a different ContentDialogHost.");
        }

        dialog.DialogHostEx ??= dialogHost;
        return dialog.ShowAsync(cancellationToken);
    }
}
