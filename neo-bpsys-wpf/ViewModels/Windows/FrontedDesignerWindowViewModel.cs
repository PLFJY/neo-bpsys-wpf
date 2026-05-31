using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Helpers;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace neo_bpsys_wpf.ViewModels.Windows;

/// <summary>
/// ViewModel for the independent v3 fronted designer editor shell.
/// </summary>
public partial class FrontedDesignerWindowViewModel : ViewModelBase
{
    private static readonly Regex ValidControlNameRegex = new(
        "^[A-Za-z_][A-Za-z0-9_]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IFrontedLayoutService _layoutService;
    private readonly FrontedLayoutDesignConverter _designConverter;
    private readonly FrontedLayoutRuntimeContractCatalog _runtimeContracts;
    private readonly FrontedLayoutValidator _validator;
    private readonly FrontedLayoutReferenceScanner _referenceScanner;
    private readonly FrontedPropertyGridBuilder _propertyGridBuilder;
    private readonly ILogger<FrontedDesignerWindowViewModel> _logger;
    private readonly Dictionary<string, string> _propertyEditErrors = new(StringComparer.Ordinal);
    private IReadOnlyList<FrontedLayoutValidationMessage> _lastValidationMessages = [];
    private bool _isChangingZoomPreset;
    private double _lastPreviewViewportWidth;
    private double _lastPreviewViewportHeight;

#pragma warning disable CS8618
    public FrontedDesignerWindowViewModel()
#pragma warning restore CS8618
    {
        // Decorative constructor for design-time only.
        _layoutService = null!;
        _designConverter = new FrontedLayoutDesignConverter();
        _runtimeContracts = new FrontedLayoutRuntimeContractCatalog();
        _referenceScanner = new FrontedLayoutReferenceScanner();
        _validator = new FrontedLayoutValidator(
            runtimeContracts: _runtimeContracts,
            referenceScanner: _referenceScanner);
        _propertyGridBuilder = new FrontedPropertyGridBuilder();
        _logger = NullLogger<FrontedDesignerWindowViewModel>.Instance;
        InitializeZoomPresets();
    }

    public FrontedDesignerWindowViewModel(
        FrontedDesignerLayoutCatalog layoutCatalog,
        IFrontedLayoutService layoutService,
        FrontedLayoutDesignConverter designConverter,
        FrontedLayoutRuntimeContractCatalog runtimeContracts,
        FrontedLayoutValidator validator,
        FrontedLayoutReferenceScanner referenceScanner,
        FrontedPropertyGridBuilder propertyGridBuilder,
        ILogger<FrontedDesignerWindowViewModel> logger)
    {
        _layoutService = layoutService;
        _designConverter = designConverter;
        _runtimeContracts = runtimeContracts;
        _validator = validator;
        _referenceScanner = referenceScanner;
        _propertyGridBuilder = propertyGridBuilder;
        _logger = logger;

        foreach (var group in layoutCatalog.GetEntries()
                     .Where(entry => entry.IsMigrated && entry.IsEditable)
                     .GroupBy(entry => entry.WindowTypeName)
                     .Select(group => new FrontedDesignerWindowOption(group.Key, group.First().DisplayName, group.ToArray())))
        {
            WindowOptions.Add(group);
        }

        InitializeZoomPresets();
        SelectedZoomPreset = ZoomPresets.FirstOrDefault();
        SelectedWindow = WindowOptions.FirstOrDefault();
    }

    /// <summary>
    /// Raised when the view should render or clear the preview canvas.
    /// </summary>
    public event EventHandler<FrontedDesignerPreviewRenderRequestedEventArgs>? PreviewRenderRequested;

    public ObservableCollection<FrontedDesignerWindowOption> WindowOptions { get; } = [];

    public ObservableCollection<FrontedDesignerLayoutCatalogEntry> CanvasOptions { get; } = [];

    public ObservableCollection<FrontedLayoutValidationMessage> ValidationMessages { get; } = [];

    public ObservableCollection<FrontedDesignerZoomPreset> ZoomPresets { get; } = [];

    public ObservableCollection<FrontedControlDesignItem> FilteredDesignItems { get; } = [];

    public ObservableCollection<FrontedPropertyEditorItem> PropertyEditorItems { get; } = [];

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
    private string _zoomDisplay = "Fit";

    [ObservableProperty]
    private bool _isFitMode = true;

    [ObservableProperty]
    private FrontedDesignerZoomPreset? _selectedZoomPreset;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedDesignItem))]
    private FrontedControlDesignItem? _selectedDesignItem;

    public bool HasSelectedDesignItem => SelectedDesignItem is not null;

    [ObservableProperty]
    private string _controlFilterText = string.Empty;

    [ObservableProperty]
    private string _selectedControlDisplay = string.Empty;

    [ObservableProperty]
    private string _selectedControlTypeDisplay = string.Empty;

    [ObservableProperty]
    private string _selectedControlGeometryDisplay = string.Empty;

    [ObservableProperty]
    private string _selectedControlRuntimeCriticalDisplay = string.Empty;

    [ObservableProperty]
    private int _selectedControlValidationMessageCount;

    [ObservableProperty]
    private int _errorCount;

    [ObservableProperty]
    private int _warningCount;

    [ObservableProperty]
    private int _infoCount;

    partial void OnSelectedWindowChanged(FrontedDesignerWindowOption? value)
    {
        ControlFilterText = string.Empty;
        SelectDesignItem(null);
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

    partial void OnSelectedCanvasChanged(FrontedDesignerLayoutCatalogEntry? value)
    {
        ControlFilterText = string.Empty;
        SelectDesignItem(null);
    }

    partial void OnCurrentDocumentChanged(FrontedCanvasDesignDocument? value)
    {
        _propertyEditErrors.Clear();
        RebuildFilteredDesignItems();
        RebuildPropertyEditorItems();
        UpdateFitZoomFromCurrentDocument();
    }

    partial void OnSelectedDesignItemChanged(FrontedControlDesignItem? value)
    {
        if (CurrentDocument is not null)
        {
            foreach (var control in CurrentDocument.Controls)
            {
                control.IsSelected = ReferenceEquals(control, value);
            }
        }

        RefreshSelectedControlDisplay();
        RebuildPropertyEditorItems();
    }

    partial void OnControlFilterTextChanged(string value)
    {
        RebuildFilteredDesignItems();
    }

    partial void OnSelectedZoomPresetChanged(FrontedDesignerZoomPreset? value)
    {
        if (_isChangingZoomPreset || value is null)
        {
            return;
        }

        ApplyZoomPreset(value);
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

            ControlFilterText = string.Empty;
            CurrentDocument = document;
            SelectDesignItem(null);
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

        ValidateCurrentDocument();
    }

    [RelayCommand]
    private void ZoomIn()
    {
        ApplyManualZoom(GetNextManualZoom(ZoomScale));
    }

    [RelayCommand]
    private void ZoomOut()
    {
        ApplyManualZoom(GetPreviousManualZoom(ZoomScale));
    }

    [RelayCommand]
    private void FitToWindow()
    {
        var fitPreset = ZoomPresets.FirstOrDefault(preset => preset.IsFit);
        if (fitPreset is not null)
        {
            ApplyZoomPreset(fitPreset);
        }
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

    public void SelectDesignItem(FrontedControlDesignItem? item)
    {
        if (item?.IsSelectableInEditor == false)
        {
            item = null;
        }

        SelectedDesignItem = item;
    }

    public void ClearSelection()
    {
        SelectDesignItem(null);
    }

    public void MoveSelectedDesignItem(
        double originalLeft,
        double originalTop,
        double deltaX,
        double deltaY,
        bool renderPreview)
    {
        if (CurrentDocument is null || SelectedDesignItem is null)
        {
            return;
        }

        FrontedDesignerGeometryHelper.Move(
            SelectedDesignItem,
            originalLeft,
            originalTop,
            deltaX,
            deltaY,
            CurrentDocument);
        SyncLinkedOverlays(SelectedDesignItem);
        OnDesignItemGeometryChanged(renderPreview);
    }

    public void MoveSelectedDesignItemBy(double deltaX, double deltaY)
    {
        if (CurrentDocument is null || SelectedDesignItem is null)
        {
            return;
        }

        FrontedDesignerGeometryHelper.MoveBy(SelectedDesignItem, deltaX, deltaY, CurrentDocument);
        SyncLinkedOverlays(SelectedDesignItem);
        OnDesignItemGeometryChanged(renderPreview: true);
    }

    public void ResizeSelectedDesignItem(
        FrontedDesignerResizeHandleKind handle,
        double originalLeft,
        double originalTop,
        double originalWidth,
        double originalHeight,
        double deltaX,
        double deltaY,
        bool renderPreview)
    {
        if (CurrentDocument is null || SelectedDesignItem is null)
        {
            return;
        }

        FrontedDesignerGeometryHelper.Resize(
            SelectedDesignItem,
            handle,
            originalLeft,
            originalTop,
            originalWidth,
            originalHeight,
            deltaX,
            deltaY,
            CurrentDocument);
        SyncLinkedOverlays(SelectedDesignItem);
        OnDesignItemGeometryChanged(renderPreview);
    }

    public IReadOnlyList<FrontedControlDesignItem> SyncLinkedOverlays(
        FrontedControlDesignItem changedTarget,
        FrontedDesignerResolvedBounds? targetBounds = null)
    {
        if (CurrentDocument is null)
        {
            return [];
        }

        return targetBounds.HasValue
            ? FrontedLayoutLinkedOverlaySynchronizer.SyncLinkedOverlays(
                CurrentDocument,
                changedTarget,
                targetBounds.Value)
            : FrontedLayoutLinkedOverlaySynchronizer.SyncLinkedOverlays(CurrentDocument, changedTarget);
    }

    public void CommitDesignItemGeometryEdit()
    {
        if (CurrentDocument is null)
        {
            return;
        }

        ValidateCurrentDocument();
        RequestPreviewRenderCurrentDocument();
    }

    public void ApplyPropertyEdit(FrontedPropertyEditorItem item, object? newValue)
    {
        if (CurrentDocument is null || SelectedDesignItem is null)
        {
            return;
        }

        if (item.IsReadOnly || item.EditorKind == FrontedPropertyEditorKind.ReadOnly)
        {
            return;
        }

        _propertyEditErrors.Remove(item.PropertyName);

        if (item.PropertyName == nameof(FrontedControlDesignItem.Name))
        {
            ApplyNameEdit(item, newValue);
            return;
        }

        var property = SelectedDesignItem.Config.GetType().GetProperty(
            item.PropertyName,
            BindingFlags.Instance | BindingFlags.Public);
        if (property is null || !property.CanWrite)
        {
            return;
        }

        if (!TryConvertPropertyValue(property, newValue, out var convertedValue, out var errorMessage))
        {
            SetPropertyEditError(item.PropertyName, errorMessage);
            return;
        }

        var oldValue = property.GetValue(SelectedDesignItem.Config);
        if (ValuesEqual(oldValue, convertedValue))
        {
            RebuildPropertyEditorItems();
            return;
        }

        property.SetValue(SelectedDesignItem.Config, convertedValue);
        CurrentDocument.IsDirty = true;

        if (IsGeometryProperty(item.PropertyName))
        {
            SyncLinkedOverlays(SelectedDesignItem);
        }

        FinishPropertyEdit(item.PropertyName);
    }

    private void ApplyNameEdit(FrontedPropertyEditorItem item, object? newValue)
    {
        if (CurrentDocument is null || SelectedDesignItem is null)
        {
            return;
        }

        if (SelectedDesignItem.IsRuntimeCritical
            || !SelectedDesignItem.IsSelectableInEditor
            || !SelectedDesignItem.IsEditableInEditor)
        {
            SetPropertyEditError(
                item.PropertyName,
                I18nHelper.GetLocalizedString("RuntimeCriticalControl"));
            return;
        }

        var oldName = SelectedDesignItem.Name;
        var newName = Convert.ToString(newValue, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
        if (oldName == newName)
        {
            RebuildPropertyEditorItems();
            return;
        }

        if (string.IsNullOrWhiteSpace(newName) || !ValidControlNameRegex.IsMatch(newName))
        {
            SetPropertyEditError(
                item.PropertyName,
                I18nHelper.GetLocalizedString("InvalidControlName"));
            return;
        }

        if (CurrentDocument.Controls.Any(control =>
                !ReferenceEquals(control, SelectedDesignItem)
                && string.Equals(control.Name, newName, StringComparison.Ordinal)))
        {
            SetPropertyEditError(
                item.PropertyName,
                I18nHelper.GetLocalizedString("DuplicateControlName"));
            return;
        }

        _referenceScanner.SetControls(CurrentDocument.Controls);
        if (_referenceScanner.GetIncomingReferences(oldName).Count > 0)
        {
            SetPropertyEditError(
                item.PropertyName,
                I18nHelper.GetLocalizedString("ReferencedControlRenameBlocked"));
            return;
        }

        SelectedDesignItem.Name = newName;
        CurrentDocument.IsDirty = true;
        FinishPropertyEdit(item.PropertyName);
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
        ControlFilterText = string.Empty;
        CurrentDocument = null;
        SelectDesignItem(null);
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
        RefreshSelectedControlDisplay();
        RebuildPropertyEditorItems();
    }

    private void ValidateCurrentDocument()
    {
        if (CurrentDocument is null || _validator is null)
        {
            return;
        }

        ApplyValidationMessages(_validator.Validate(CurrentDocument));
    }

    private void RebuildPropertyEditorItems()
    {
        PropertyEditorItems.Clear();

        if (CurrentDocument is null || SelectedDesignItem is null)
        {
            return;
        }

        var rows = _propertyGridBuilder.Build(
            CurrentDocument,
            SelectedDesignItem,
            _validator,
            _referenceScanner,
            _runtimeContracts);

        foreach (var row in rows)
        {
            row.DisplayName = I18nHelper.GetLocalizedString(row.DisplayName);
            if (!string.IsNullOrWhiteSpace(row.GroupName))
            {
                row.GroupDisplayName = I18nHelper.GetLocalizedString(row.GroupName);
            }

            if (_propertyEditErrors.TryGetValue(row.PropertyName, out var editError))
            {
                row.ValidationErrors = row.ValidationErrors
                    .Concat([editError])
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
            }

            PropertyEditorItems.Add(row);
        }
    }

    private void SetPropertyEditError(string propertyName, string message)
    {
        _propertyEditErrors[propertyName] = message;
        StatusMessage = message;
        RebuildPropertyEditorItems();
    }

    private void FinishPropertyEdit(string propertyName)
    {
        _propertyEditErrors.Remove(propertyName);
        DirtyIndicatorText = CurrentDocument?.IsDirty == true ? "●" : "○";
        RebuildFilteredDesignItems();
        ValidateCurrentDocument();
        RequestPreviewRenderCurrentDocument();
    }

    private void OnDesignItemGeometryChanged(bool renderPreview)
    {
        DirtyIndicatorText = CurrentDocument?.IsDirty == true ? "●" : "○";
        RefreshSelectedControlDisplay();
        ValidateCurrentDocument();

        if (renderPreview)
        {
            RequestPreviewRenderCurrentDocument();
        }
    }

    private void RequestPreviewRenderCurrentDocument()
    {
        if (CurrentDocument is null)
        {
            RequestPreviewRender(null, SelectedCanvas);
            return;
        }

        RequestPreviewRender(_designConverter.ToConfig(CurrentDocument), SelectedCanvas);
    }

    private void RebuildFilteredDesignItems()
    {
        FilteredDesignItems.Clear();

        if (CurrentDocument is null)
        {
            return;
        }

        var filter = ControlFilterText?.Trim();
        var controls = CurrentDocument.Controls
            .Select((item, index) => new { Item = item, Index = index })
            .Where(entry => entry.Item.IsSelectableInEditor && MatchesControlFilter(entry.Item, filter))
            .OrderByDescending(entry => entry.Item.Config.ZIndex)
            .ThenBy(entry => entry.Index)
            .Select(entry => entry.Item);

        foreach (var item in controls)
        {
            FilteredDesignItems.Add(item);
        }
    }

    public static bool MatchesControlFilter(FrontedControlDesignItem item, string? filter)
    {
        if (!item.IsSelectableInEditor)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        return item.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
               || item.Config.ControlType.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshSelectedControlDisplay()
    {
        if (SelectedDesignItem is null)
        {
            SelectedControlDisplay = I18nHelper.GetLocalizedString("NoControlSelected");
            SelectedControlTypeDisplay = string.Empty;
            SelectedControlGeometryDisplay = string.Empty;
            SelectedControlRuntimeCriticalDisplay = string.Empty;
            SelectedControlValidationMessageCount = 0;
            return;
        }

        var config = SelectedDesignItem.Config;
        SelectedControlDisplay = SelectedDesignItem.Name;
        SelectedControlTypeDisplay = config.ControlType;
        SelectedControlGeometryDisplay =
            $"L {config.Left:0.##}  T {config.Top:0.##}  "
            + $"W {(config.Width?.ToString("0.##") ?? "-")}  "
            + $"H {(config.Height?.ToString("0.##") ?? "-")}";
        SelectedControlRuntimeCriticalDisplay = SelectedDesignItem.IsRuntimeCritical
            ? I18nHelper.GetLocalizedString("RuntimeCriticalControl")
            : string.Empty;
        SelectedControlValidationMessageCount = SelectedDesignItem.ValidationMessages.Count;
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

    private void ApplyZoomPreset(FrontedDesignerZoomPreset preset)
    {
        if (preset.IsFit)
        {
            IsFitMode = true;
            UpdateFitZoomFromCurrentDocument();
        }
        else
        {
            ApplyManualZoom(preset.Scale);
        }

        SetSelectedZoomPreset(preset);
    }

    private void ApplyManualZoom(double scale)
    {
        var normalizedScale = Math.Clamp(scale, 0.25D, 2D);
        IsFitMode = false;
        ZoomScale = normalizedScale;
        ZoomDisplay = $"{normalizedScale:P0}";

        var matchingPreset = ZoomPresets.FirstOrDefault(
            preset => !preset.IsFit && Math.Abs(preset.Scale - normalizedScale) < 0.001D);
        SetSelectedZoomPreset(matchingPreset);
    }

    public void ZoomByWheelDelta(int delta)
    {
        if (delta == 0)
        {
            return;
        }

        var multiplier = delta > 0 ? 1.1D : 1D / 1.1D;
        ApplyManualZoom(ZoomScale * multiplier);
    }

    public void UpdateFitZoom(double viewportWidth, double viewportHeight)
    {
        _lastPreviewViewportWidth = viewportWidth;
        _lastPreviewViewportHeight = viewportHeight;
        UpdateFitZoomFromCurrentDocument();
    }

    public void UpdateFitZoom(
        double viewportWidth,
        double viewportHeight,
        double canvasWidth,
        double canvasHeight)
    {
        _lastPreviewViewportWidth = viewportWidth;
        _lastPreviewViewportHeight = viewportHeight;

        if (!IsFitMode)
        {
            return;
        }

        ApplyFitZoom(viewportWidth, viewportHeight, canvasWidth, canvasHeight);
    }

    public static double CalculateFitZoom(
        double viewportWidth,
        double viewportHeight,
        double canvasWidth,
        double canvasHeight,
        double padding = 0D)
    {
        if (canvasWidth <= 0D || canvasHeight <= 0D)
        {
            return 1D;
        }

        var availableWidth = Math.Max(1D, viewportWidth - padding);
        var availableHeight = Math.Max(1D, viewportHeight - padding);
        var scale = Math.Min(availableWidth / canvasWidth, availableHeight / canvasHeight);
        return Math.Clamp(scale, 0.05D, 4D);
    }

    private double GetNextManualZoom(double currentScale)
    {
        return ZoomPresets
            .Where(preset => !preset.IsFit && preset.Scale > currentScale + 0.001D)
            .OrderBy(preset => preset.Scale)
            .FirstOrDefault()?.Scale ?? 2D;
    }

    private double GetPreviousManualZoom(double currentScale)
    {
        return ZoomPresets
            .Where(preset => !preset.IsFit && preset.Scale < currentScale - 0.001D)
            .OrderByDescending(preset => preset.Scale)
            .FirstOrDefault()?.Scale ?? 0.25D;
    }

    private void InitializeZoomPresets()
    {
        if (ZoomPresets.Count > 0)
        {
            return;
        }

        ZoomPresets.Add(new FrontedDesignerZoomPreset("Fit", 0D, isFit: true));
        ZoomPresets.Add(new FrontedDesignerZoomPreset("25%", 0.25D));
        ZoomPresets.Add(new FrontedDesignerZoomPreset("50%", 0.5D));
        ZoomPresets.Add(new FrontedDesignerZoomPreset("75%", 0.75D));
        ZoomPresets.Add(new FrontedDesignerZoomPreset("100%", 1D));
        ZoomPresets.Add(new FrontedDesignerZoomPreset("125%", 1.25D));
        ZoomPresets.Add(new FrontedDesignerZoomPreset("150%", 1.5D));
        ZoomPresets.Add(new FrontedDesignerZoomPreset("200%", 2D));
    }

    private void UpdateFitZoomFromCurrentDocument()
    {
        if (!IsFitMode)
        {
            return;
        }

        var canvas = CurrentDocument?.CanvasConfig;
        if (canvas is null)
        {
            ZoomScale = 1D;
            ZoomDisplay = I18nHelper.GetLocalizedString("Fit");
            return;
        }

        ApplyFitZoom(
            _lastPreviewViewportWidth,
            _lastPreviewViewportHeight,
            canvas.CanvasWidth,
            canvas.CanvasHeight);
    }

    private void ApplyFitZoom(
        double viewportWidth,
        double viewportHeight,
        double canvasWidth,
        double canvasHeight)
    {
        ZoomScale = CalculateFitZoom(viewportWidth, viewportHeight, canvasWidth, canvasHeight);
        ZoomDisplay = $"{I18nHelper.GetLocalizedString("Fit")} ({ZoomScale:P0})";
    }

    private void SetSelectedZoomPreset(FrontedDesignerZoomPreset? preset)
    {
        _isChangingZoomPreset = true;
        SelectedZoomPreset = preset;
        _isChangingZoomPreset = false;
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

    private static bool TryConvertPropertyValue(
        PropertyInfo property,
        object? value,
        out object? convertedValue,
        out string errorMessage)
    {
        convertedValue = null;
        errorMessage = string.Empty;

        var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        var text = Convert.ToString(value, CultureInfo.InvariantCulture);

        if (Nullable.GetUnderlyingType(property.PropertyType) is not null
            && string.IsNullOrWhiteSpace(text))
        {
            convertedValue = null;
            return true;
        }

        try
        {
            if (targetType == typeof(string))
            {
                convertedValue = text;
            }
            else if (targetType == typeof(bool))
            {
                convertedValue = value is bool boolValue
                    ? boolValue
                    : bool.Parse(text ?? string.Empty);
            }
            else if (targetType.IsEnum)
            {
                convertedValue = value?.GetType() == targetType
                    ? value
                    : Enum.Parse(targetType, text ?? string.Empty, ignoreCase: true);
            }
            else if (targetType == typeof(int))
            {
                convertedValue = Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            else if (targetType == typeof(long))
            {
                convertedValue = Convert.ToInt64(value, CultureInfo.InvariantCulture);
            }
            else if (targetType == typeof(float))
            {
                convertedValue = Convert.ToSingle(value, CultureInfo.InvariantCulture);
            }
            else if (targetType == typeof(decimal))
            {
                convertedValue = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            }
            else if (targetType == typeof(double))
            {
                var doubleValue = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                if (double.IsNaN(doubleValue) || double.IsInfinity(doubleValue))
                {
                    errorMessage = I18nHelper.GetLocalizedString("PropertyValidationErrors");
                    return false;
                }

                convertedValue = NormalizeDoubleProperty(property.Name, doubleValue);
            }
            else
            {
                errorMessage = I18nHelper.GetLocalizedString("PropertyValidationErrors");
                return false;
            }

            return true;
        }
        catch (Exception)
        {
            errorMessage = I18nHelper.GetLocalizedString("PropertyValidationErrors");
            return false;
        }
    }

    private static double NormalizeDoubleProperty(string propertyName, double value)
    {
        if (propertyName is nameof(FrontedControlConfigBase.Left)
            or nameof(FrontedControlConfigBase.Top))
        {
            return FrontedDesignerGeometryHelper.Snap(value);
        }

        if (propertyName is nameof(FrontedControlConfigBase.Width)
            or nameof(FrontedControlConfigBase.Height))
        {
            return Math.Max(
                FrontedDesignerGeometryHelper.MinResizeWidth,
                FrontedDesignerGeometryHelper.Snap(value));
        }

        return value;
    }

    private static bool ValuesEqual(object? left, object? right)
    {
        if (left is double leftDouble && right is double rightDouble)
        {
            return Math.Abs(leftDouble - rightDouble) < 0.0001D;
        }

        return Equals(left, right);
    }

    private static bool IsGeometryProperty(string propertyName)
    {
        return propertyName is nameof(FrontedControlConfigBase.Left)
            or nameof(FrontedControlConfigBase.Top)
            or nameof(FrontedControlConfigBase.Width)
            or nameof(FrontedControlConfigBase.Height);
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

public sealed class FrontedDesignerZoomPreset(string displayName, double scale, bool isFit = false)
{
    public string DisplayName { get; } = displayName;

    public double Scale { get; } = scale;

    public bool IsFit { get; } = isFit;
}

public sealed class FrontedDesignerPreviewRenderRequestedEventArgs(
    FrontedCanvasConfig? config,
    FrontedRenderContext? context) : EventArgs
{
    public FrontedCanvasConfig? Config { get; } = config;

    public FrontedRenderContext? Context { get; } = context;
}
