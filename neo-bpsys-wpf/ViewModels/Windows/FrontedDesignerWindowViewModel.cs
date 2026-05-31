using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Helpers;
using System.Collections.ObjectModel;
using System.IO;

namespace neo_bpsys_wpf.ViewModels.Windows;

/// <summary>
/// ViewModel for the independent v3 fronted designer editor shell.
/// </summary>
public partial class FrontedDesignerWindowViewModel : ViewModelBase
{
    private readonly IFrontedLayoutService _layoutService;
    private readonly FrontedLayoutDesignConverter _designConverter;
    private readonly FrontedLayoutRuntimeContractCatalog _runtimeContracts;
    private readonly FrontedLayoutValidator _validator;
    private readonly ILogger<FrontedDesignerWindowViewModel> _logger;
    private IReadOnlyList<FrontedLayoutValidationMessage> _lastValidationMessages = [];

#pragma warning disable CS8618
    public FrontedDesignerWindowViewModel()
#pragma warning restore CS8618
    {
        // Decorative constructor for design-time only.
    }

    public FrontedDesignerWindowViewModel(
        FrontedDesignerLayoutCatalog layoutCatalog,
        IFrontedLayoutService layoutService,
        FrontedLayoutDesignConverter designConverter,
        FrontedLayoutRuntimeContractCatalog runtimeContracts,
        FrontedLayoutValidator validator,
        ILogger<FrontedDesignerWindowViewModel> logger)
    {
        _layoutService = layoutService;
        _designConverter = designConverter;
        _runtimeContracts = runtimeContracts;
        _validator = validator;
        _logger = logger;

        foreach (var group in layoutCatalog.GetEntries()
                     .Where(entry => entry.IsMigrated && entry.IsEditable)
                     .GroupBy(entry => entry.WindowTypeName)
                     .Select(group => new FrontedDesignerWindowOption(group.Key, group.First().DisplayName, group.ToArray())))
        {
            WindowOptions.Add(group);
        }

        SelectedWindow = WindowOptions.FirstOrDefault();
    }

    /// <summary>
    /// Raised when the view should render or clear the preview canvas.
    /// </summary>
    public event EventHandler<FrontedDesignerPreviewRenderRequestedEventArgs>? PreviewRenderRequested;

    public ObservableCollection<FrontedDesignerWindowOption> WindowOptions { get; } = [];

    public ObservableCollection<FrontedDesignerLayoutCatalogEntry> CanvasOptions { get; } = [];

    public ObservableCollection<FrontedLayoutValidationMessage> ValidationMessages { get; } = [];

    [ObservableProperty]
    private FrontedDesignerWindowOption? _selectedWindow;

    [ObservableProperty]
    private FrontedDesignerLayoutCatalogEntry? _selectedCanvas;

    [ObservableProperty]
    private FrontedCanvasDesignDocument? _currentDocument;

    [ObservableProperty]
    private string _layoutSourcePath = string.Empty;

    [ObservableProperty]
    private string _layoutSourceDisplay = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _currentWindowCanvasDisplay = string.Empty;

    [ObservableProperty]
    private string _dirtyIndicatorText = "○";

    [ObservableProperty]
    private double _zoomScale = 1D;

    [ObservableProperty]
    private string _zoomDisplay = "100%";

    [ObservableProperty]
    private int _errorCount;

    [ObservableProperty]
    private int _warningCount;

    [ObservableProperty]
    private int _infoCount;

    partial void OnSelectedWindowChanged(FrontedDesignerWindowOption? value)
    {
        CanvasOptions.Clear();

        if (value is null)
        {
            SelectedCanvas = null;
            return;
        }

        foreach (var canvas in value.Canvases)
        {
            CanvasOptions.Add(canvas);
        }

        SelectedCanvas = CanvasOptions.FirstOrDefault();
    }

    [RelayCommand]
    private async Task ReloadLayoutAsync()
    {
        if (SelectedWindow is null || SelectedCanvas is null)
        {
            ClearLoadedLayout(CreateMessage(
                FrontedLayoutValidationSeverity.Error,
                "LayoutSelectionMissing",
                "Window and Canvas selection are required."));
            return;
        }

        var entry = SelectedCanvas;
        CurrentWindowCanvasDisplay = $"{entry.WindowTypeName} / {entry.CanvasName}";
        DirtyIndicatorText = "○";
        ResolveLayoutSource(entry);

        try
        {
            var config = await _layoutService.LoadCanvasConfigAsync(entry.WindowTypeName, entry.CanvasName);
            if (config is null)
            {
                ClearLoadedLayout(CreateMessage(
                    FrontedLayoutValidationSeverity.Error,
                    "MissingLayout",
                    $"Layout file was not found for {entry.WindowTypeName}/{entry.CanvasName}."));
                return;
            }

            var document = _designConverter.FromConfig(
                entry.WindowTypeName,
                entry.CanvasName,
                config,
                _runtimeContracts);

            CurrentDocument = document;
            ApplyValidationMessages(_validator.Validate(document));
            RequestPreviewRender(config, entry);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to load fronted designer layout. Window: {WindowTypeName}, Canvas: {CanvasName}",
                entry.WindowTypeName,
                entry.CanvasName);

            ClearLoadedLayout(CreateMessage(
                FrontedLayoutValidationSeverity.Error,
                "LayoutLoadFailed",
                ex.Message));
        }
    }

    [RelayCommand]
    private void ValidateLayout()
    {
        if (CurrentDocument is null)
        {
            ApplyValidationMessages(_lastValidationMessages);
            return;
        }

        ApplyValidationMessages(_validator.Validate(CurrentDocument));
    }

    /// <summary>
    /// Adds a non-fatal render error to the validation/status panel.
    /// </summary>
    public void ReportRenderFailure(Exception exception)
    {
        _logger.LogError(exception, "Failed to render fronted designer preview.");

        var messages = _lastValidationMessages
            .Concat(
            [
                CreateMessage(
                    FrontedLayoutValidationSeverity.Error,
                    "RenderFailed",
                    exception.Message)
            ])
            .ToArray();

        ApplyValidationMessages(messages);
        StatusMessage = exception.Message;
    }

    private void ResolveLayoutSource(FrontedDesignerLayoutCatalogEntry entry)
    {
        var userPath = _layoutService.GetUserLayoutPath(entry.WindowTypeName, entry.CanvasName);
        if (File.Exists(userPath))
        {
            LayoutSourceDisplay = I18nHelper.GetLocalizedString("UserLayout");
            LayoutSourcePath = userPath;
            return;
        }

        var builtInPath = _layoutService.GetBuiltInDefaultLayoutPath(entry.WindowTypeName, entry.CanvasName);
        if (File.Exists(builtInPath))
        {
            LayoutSourceDisplay = I18nHelper.GetLocalizedString("BuiltInLayout");
            LayoutSourcePath = builtInPath;
            return;
        }

        LayoutSourceDisplay = I18nHelper.GetLocalizedString("MissingLayout");
        LayoutSourcePath = builtInPath;
    }

    private void ClearLoadedLayout(FrontedLayoutValidationMessage message)
    {
        CurrentDocument = null;
        ApplyValidationMessages([message]);
        RequestPreviewRender(null, SelectedCanvas);
    }

    private void ApplyValidationMessages(IReadOnlyList<FrontedLayoutValidationMessage> messages)
    {
        _lastValidationMessages = messages;
        ValidationMessages.Clear();
        foreach (var message in messages)
        {
            ValidationMessages.Add(message);
        }

        ErrorCount = messages.Count(message => message.Severity == FrontedLayoutValidationSeverity.Error);
        WarningCount = messages.Count(message => message.Severity == FrontedLayoutValidationSeverity.Warning);
        InfoCount = messages.Count(message => message.Severity == FrontedLayoutValidationSeverity.Info);
        StatusMessage =
            $"{I18nHelper.GetLocalizedString("Errors")}: {ErrorCount}  "
            + $"{I18nHelper.GetLocalizedString("Warnings")}: {WarningCount}  "
            + $"{I18nHelper.GetLocalizedString("Infos")}: {InfoCount}";
    }

    private void RequestPreviewRender(FrontedCanvasConfig? config, FrontedDesignerLayoutCatalogEntry? entry)
    {
        PreviewRenderRequested?.Invoke(
            this,
            new FrontedDesignerPreviewRenderRequestedEventArgs(
                config,
                entry is null
                    ? null
                    : new FrontedRenderContext
                    {
                        WindowId = entry.WindowId,
                        CanvasName = entry.CanvasName
                    }));
    }

    private static FrontedLayoutValidationMessage CreateMessage(
        FrontedLayoutValidationSeverity severity,
        string code,
        string message)
    {
        return new FrontedLayoutValidationMessage
        {
            Severity = severity,
            Code = code,
            Message = message
        };
    }
}

public sealed class FrontedDesignerWindowOption(
    string windowTypeName,
    string displayName,
    IReadOnlyList<FrontedDesignerLayoutCatalogEntry> canvases)
{
    public string WindowTypeName { get; } = windowTypeName;

    public string DisplayName { get; } = displayName;

    public IReadOnlyList<FrontedDesignerLayoutCatalogEntry> Canvases { get; } = canvases;
}

public sealed class FrontedDesignerPreviewRenderRequestedEventArgs(
    FrontedCanvasConfig? config,
    FrontedRenderContext? context) : EventArgs
{
    public FrontedCanvasConfig? Config { get; } = config;

    public FrontedRenderContext? Context { get; } = context;
}
