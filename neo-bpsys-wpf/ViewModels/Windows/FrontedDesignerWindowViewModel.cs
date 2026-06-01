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
using neo_bpsys_wpf.Services.FrontedDesigner;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;

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
    private readonly FrontedControlDefaultConfigFactory _defaultConfigFactory;
    private readonly FrontedControlNameGenerator _controlNameGenerator;
    private readonly IFrontedDesignerLocalizationService _localizationService;
    private readonly ISharedDataService _designerPreviewSharedDataService;
    private readonly IFrontedLocalResourceStore? _localResourceStore;
    private readonly IFrontedWindowLayoutOptionsService? _windowLayoutOptionsService;
    private readonly ILogger<FrontedDesignerWindowViewModel> _logger;
    private readonly Dictionary<string, string> _propertyEditErrors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _propertyEditBuffers = new(StringComparer.Ordinal);
    private readonly Stack<string> _undoStack = new();
    private readonly Stack<string> _redoStack = new();
    private FrontedDesignerClipboardPayload? _copiedControl;
    private IReadOnlyList<FrontedLayoutValidationMessage> _lastValidationMessages = [];
    private bool _isChangingZoomPreset;
    private bool _suppressZoomEditTextUpdate;
    private bool _isRebuildingPropertyGrid;
    private bool _isRestoringSnapshot;
    private bool _isLoadingWindowOptions;
    private bool _scheduledValidationAndPreviewPending;
    private bool _scheduledValidationRequested;
    private bool _scheduledPreviewRequested;
    private FrontedControlDesignItem? _lastSelectedDesignItem;
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
        _defaultConfigFactory = new FrontedControlDefaultConfigFactory();
        _controlNameGenerator = new FrontedControlNameGenerator();
        _localizationService = new FrontedDesignerLocalizationService();
        _designerPreviewSharedDataService = new DesignerPreviewSharedDataService();
        _localResourceStore = null;
        _windowLayoutOptionsService = null;
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
        FrontedControlDefaultConfigFactory defaultConfigFactory,
        FrontedControlNameGenerator controlNameGenerator,
        IFrontedDesignerLocalizationService localizationService,
        DesignerPreviewSharedDataService designerPreviewSharedDataService,
        IFrontedLocalResourceStore localResourceStore,
        IFrontedWindowLayoutOptionsService windowLayoutOptionsService,
        ILogger<FrontedDesignerWindowViewModel> logger)
    {
        _layoutService = layoutService;
        _designConverter = designConverter;
        _runtimeContracts = runtimeContracts;
        _validator = validator;
        _referenceScanner = referenceScanner;
        _propertyGridBuilder = propertyGridBuilder;
        _defaultConfigFactory = defaultConfigFactory;
        _controlNameGenerator = controlNameGenerator;
        _localizationService = localizationService;
        _designerPreviewSharedDataService = designerPreviewSharedDataService;
        _localResourceStore = localResourceStore;
        _windowLayoutOptionsService = windowLayoutOptionsService;
        _logger = logger;

        foreach (var group in layoutCatalog.GetEntries()
                     .Where(entry => entry.IsMigrated && entry.IsEditable)
                     .GroupBy(entry => entry.WindowTypeName)
                     .Select(group => new FrontedDesignerWindowOption(
                         group.Key,
                         _localizationService.GetWindowDisplayName(group.Key),
                         group
                             .Select(entry => new FrontedDesignerLayoutCatalogEntry
                             {
                                 WindowTypeName = entry.WindowTypeName,
                                 DisplayName = _localizationService.GetWindowDisplayName(entry.WindowTypeName),
                                 WindowId = entry.WindowId,
                                 CanvasName = entry.CanvasName,
                                 CanvasDisplayName = _localizationService.GetCanvasDisplayName(entry.CanvasName),
                                 CanvasWidth = entry.CanvasWidth,
                                 CanvasHeight = entry.CanvasHeight,
                                 IsMigrated = entry.IsMigrated,
                                 IsEditable = entry.IsEditable
                             })
                             .ToArray())))
        {
            WindowOptions.Add(group);
        }

        InitializeZoomPresets();
        RebuildAddControlCatalog();
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

    public ObservableCollection<FrontedLayerGroup> LayerGroups { get; } = [];

    public ObservableCollection<FrontedPropertyEditorItem> PropertyEditorItems { get; } = [];

    public ObservableCollection<FrontedAddControlCatalogGroup> AddControlCatalogGroups { get; } = [];

    public bool IsRebuildingPropertyGrid => _isRebuildingPropertyGrid;

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
    [NotifyPropertyChangedFor(nameof(EffectiveSnapEnabled))]
    private bool _snapEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveSnapEnabled))]
    private bool _isShiftSnapActive;

    [ObservableProperty]
    private double _snapGridSize = FrontedDesignerGeometryHelper.DefaultSnapGridSize;

    public bool EffectiveSnapEnabled => SnapEnabled || IsShiftSnapActive;

    public string SnapStatusText
    {
        get
        {
            if (SnapEnabled)
            {
                return I18nHelper.GetLocalizedString("SnapOn");
            }

            return IsShiftSnapActive
                ? I18nHelper.GetLocalizedString("TemporarySnap")
                : I18nHelper.GetLocalizedString("SnapOff");
        }
    }

    [ObservableProperty]
    private double _zoomScale = 1D;

    [ObservableProperty]
    private string _zoomDisplay = "Fit";

    [ObservableProperty]
    private bool _isFitMode = true;

    [ObservableProperty]
    private string _zoomEditText = "Fit";

    public double ZoomPercent
    {
        get => ZoomScale * 100;
        set
        {
            var clamped = Math.Clamp(value, 25D, 200D);
            ApplyManualZoom(clamped / 100D);
        }
    }

    [ObservableProperty]
    private FrontedDesignerZoomPreset? _selectedZoomPreset;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedDesignItem))]
    [NotifyPropertyChangedFor(nameof(CanDeleteSelectedControl))]
    [NotifyPropertyChangedFor(nameof(IsBorderedImageSelected))]
    private FrontedControlDesignItem? _selectedDesignItem;

    public bool HasSelectedDesignItem => SelectedDesignItem is not null;

    public bool IsBorderedImageSelected => SelectedDesignItem?.Config is BorderedImageFrontedControlConfig;

    private FrontedDesignerResizeTarget _borderedImageResizeTarget = FrontedDesignerResizeTarget.Border;

    public FrontedDesignerResizeTarget BorderedImageResizeTarget
    {
        get => _borderedImageResizeTarget;
        set
        {
            if (SetProperty(ref _borderedImageResizeTarget, value))
            {
                OnPropertyChanged(nameof(IsBorderResizeTargetSelected));
                OnPropertyChanged(nameof(IsImageResizeTargetSelected));
            }
        }
    }

    public bool IsBorderResizeTargetSelected
    {
        get => BorderedImageResizeTarget == FrontedDesignerResizeTarget.Border;
        set
        {
            if (value)
            {
                BorderedImageResizeTarget = FrontedDesignerResizeTarget.Border;
            }
        }
    }

    public bool IsImageResizeTargetSelected
    {
        get => BorderedImageResizeTarget == FrontedDesignerResizeTarget.Image;
        set
        {
            if (value)
            {
                BorderedImageResizeTarget = FrontedDesignerResizeTarget.Image;
            }
        }
    }

    public bool CanDeleteSelectedControl =>
        SelectedDesignItem is { IsSelectableInEditor: true, IsEditableInEditor: true };

    public bool CanCopySelectedControl => CanCopyControl(SelectedDesignItem);

    public bool CanPasteControl => CurrentDocument is not null && _copiedControl is not null;

    public bool HasPendingScheduledDesignerWork => _scheduledValidationAndPreviewPending;

    public int ScheduledDesignerValidationExecutionCount { get; private set; }

    public int ScheduledDesignerPreviewExecutionCount { get; private set; }

    public bool CanUndo => _undoStack.Count > 0;

    public bool CanRedo => _redoStack.Count > 0;

    public bool CanSaveLayout => CurrentDocument?.IsDirty == true;

    public bool CanResetToBuiltIn => CurrentDocument is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanReorderLayers))]
    private string _controlFilterText = string.Empty;

    public bool CanReorderLayers => CurrentDocument is not null && string.IsNullOrWhiteSpace(ControlFilterText);

    public string LayerReorderHint => CanReorderLayers
        ? string.Empty
        : I18nHelper.GetLocalizedString("Designer.LayerPanel.ClearFilterToReorder");

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

    [ObservableProperty]
    private string _canvasWidthEditText = string.Empty;

    [ObservableProperty]
    private string _canvasHeightEditText = string.Empty;

    [ObservableProperty]
    private string _backgroundImageEditText = string.Empty;

    [ObservableProperty]
    private string _canvasPropertiesStatus = string.Empty;

    [ObservableProperty]
    private string _windowOptionsWindowTypeName = string.Empty;

    [ObservableProperty]
    private bool _windowAllowTransparency;

    [ObservableProperty]
    private bool _windowOptionsRestartRequired;

    [ObservableProperty]
    private string _windowOptionsStatus = string.Empty;

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
        LoadWindowOptions(value.WindowTypeName);
    }

    partial void OnSelectedCanvasChanged(FrontedDesignerLayoutCatalogEntry? value)
    {
        ControlFilterText = string.Empty;
        SelectDesignItem(null);
    }

    partial void OnCurrentDocumentChanged(FrontedCanvasDesignDocument? value)
    {
        _propertyEditErrors.Clear();
        if (!_isRestoringSnapshot)
        {
            ClearUndoRedo();
        }
        NormalizeSelectionState();
        RefreshCanvasPropertyBuffers();
        RebuildFilteredDesignItems();
        OnPropertyChanged(nameof(CanReorderLayers));
        OnPropertyChanged(nameof(LayerReorderHint));
        RebuildPropertyEditorItems();
        UpdateFitZoomFromCurrentDocument();
        DeleteSelectedControlCommand.NotifyCanExecuteChanged();
        CopySelectedControlCommand.NotifyCanExecuteChanged();
        PasteControlCommand.NotifyCanExecuteChanged();
        NotifyLayoutCommandState();
    }

    partial void OnSnapEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(EffectiveSnapEnabled));
        OnPropertyChanged(nameof(SnapStatusText));
    }

    partial void OnIsShiftSnapActiveChanged(bool value)
    {
        OnPropertyChanged(nameof(EffectiveSnapEnabled));
        OnPropertyChanged(nameof(SnapStatusText));
    }

    partial void OnZoomScaleChanged(double value)
    {
        OnPropertyChanged(nameof(ZoomPercent));
        if (!_suppressZoomEditTextUpdate)
        {
            UpdateZoomEditTextFromCurrentZoom();
        }
    }

    partial void OnIsFitModeChanged(bool value)
    {
        if (!_suppressZoomEditTextUpdate)
        {
            UpdateZoomEditTextFromCurrentZoom();
        }
    }

    partial void OnSelectedDesignItemChanged(FrontedControlDesignItem? value)
    {
        _propertyEditErrors.Clear();
        _propertyEditBuffers.Clear();
        if (_lastSelectedDesignItem is not null
            && !ReferenceEquals(_lastSelectedDesignItem, value))
        {
            _lastSelectedDesignItem.IsSelected = false;
        }

        if (value is not null)
        {
            value.IsSelected = true;
        }

        _lastSelectedDesignItem = value;

        RefreshSelectedControlDisplay();
        RebuildPropertyEditorItems();
        DeleteSelectedControlCommand.NotifyCanExecuteChanged();
        CopySelectedControlCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsBorderedImageSelected));
        OnPropertyChanged(nameof(IsBorderResizeTargetSelected));
        OnPropertyChanged(nameof(IsImageResizeTargetSelected));
    }

    partial void OnControlFilterTextChanged(string value)
    {
        var clamped = FrontedTextLimitHelper.Clamp(value, FrontedLayoutLimits.MaxSearchTextLength);
        if (!string.Equals(value, clamped, StringComparison.Ordinal))
        {
            ControlFilterText = clamped;
            StatusMessage = I18nHelper.GetLocalizedString("InputTruncated");
            return;
        }

        RebuildFilteredDesignItems();
        OnPropertyChanged(nameof(CanReorderLayers));
        OnPropertyChanged(nameof(LayerReorderHint));
    }

    partial void OnSelectedZoomPresetChanged(FrontedDesignerZoomPreset? value)
    {
        if (_isChangingZoomPreset || value is null)
        {
            return;
        }

        ApplyZoomPreset(value);
    }

    partial void OnWindowAllowTransparencyChanged(bool value)
    {
        if (_isLoadingWindowOptions || _windowLayoutOptionsService is null || SelectedWindow is null)
        {
            return;
        }

        _ = SaveWindowOptionsAsync(value);
    }

    [RelayCommand]
    private async Task ReloadLayoutAsync()
    {
        await ReloadLayoutCoreAsync();
    }

    public async Task ReloadLayoutCoreAsync()
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
        DirtyIndicatorText = string.Empty;

        try
        {
            var loadResult = await _layoutService.LoadCanvasConfigWithMetadataAsync(entry.WindowTypeName, entry.CanvasName);
            ApplyLayoutSource(loadResult, entry);

            var config = loadResult.Config;
            if (config is null)
            {
                ClearLoadedLayout(CreateMessage(
                    FrontedLayoutValidationSeverity.Error,
                    "MissingLayout",
                    loadResult.Error ?? $"Layout file was not found for {entry.WindowTypeName}/{entry.CanvasName}."));
                return;
            }

            var document = _designConverter.FromConfig(
                entry.WindowTypeName,
                entry.CanvasName,
                config,
                _runtimeContracts);

            ControlFilterText = string.Empty;
            CurrentDocument = document;
            CurrentDocument.IsDirty = false;
            SelectDesignItem(null);
            var validationMessages = _validator.Validate(document).ToList();
            if (!string.IsNullOrWhiteSpace(loadResult.Error))
            {
                validationMessages.Add(CreateMessage(
                    FrontedLayoutValidationSeverity.Warning,
                    "UserLayoutLoadFailed",
                    loadResult.Error));
            }

            ApplyValidationMessages(validationMessages);
            RequestPreviewRender(config, entry);
            RefreshDirtyState();
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

    [RelayCommand(CanExecute = nameof(CanSaveLayout))]
    private async Task SaveLayoutAsync()
    {
        await SaveCurrentLayoutAsync();
    }

    public async Task<bool> SaveCurrentLayoutAsync()
    {
        if (CurrentDocument is null)
        {
            return false;
        }

        var messages = _validator.Validate(CurrentDocument);
        ApplyValidationMessages(messages);
        if (messages.Any(message => message.Severity == FrontedLayoutValidationSeverity.Error))
        {
            StatusMessage = I18nHelper.GetLocalizedString("CannotSaveInvalidLayout");
            return false;
        }

        try
        {
            var config = _designConverter.ToConfig(CurrentDocument);
            config.Version = 3;
            await _layoutService.SaveCanvasConfigAsync(
                CurrentDocument.WindowTypeName,
                CurrentDocument.CanvasName,
                config);

            CurrentDocument.IsDirty = false;
            LayoutSourceDisplay = I18nHelper.GetLocalizedString("LayoutSourceUser");
            LayoutSourcePath = _layoutService.GetUserLayoutPath(
                CurrentDocument.WindowTypeName,
                CurrentDocument.CanvasName);
            StatusMessage = I18nHelper.GetLocalizedString("LayoutSaved");
            RefreshDirtyState();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to save fronted designer user layout. Window: {WindowTypeName}, Canvas: {CanvasName}",
                CurrentDocument.WindowTypeName,
                CurrentDocument.CanvasName);
            StatusMessage = $"{I18nHelper.GetLocalizedString("LayoutSaveFailed")}: {ex.Message}";
            return false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanResetToBuiltIn))]
    private async Task ResetToBuiltInAsync()
    {
        await ResetToBuiltInCoreAsync();
    }

    public async Task<bool> ResetToBuiltInCoreAsync()
    {
        if (CurrentDocument is null)
        {
            return false;
        }

        var windowTypeName = CurrentDocument.WindowTypeName;
        var canvasName = CurrentDocument.CanvasName;
        await _layoutService.DeleteUserLayoutAsync(windowTypeName, canvasName);

        var config = await LoadBuiltInLayoutForResetAsync(windowTypeName, canvasName);
        if (config is null)
        {
            StatusMessage = I18nHelper.GetLocalizedString("MissingLayout");
            return false;
        }

        var document = _designConverter.FromConfig(
            windowTypeName,
            canvasName,
            config,
            _runtimeContracts);
        document.IsDirty = false;

        ControlFilterText = string.Empty;
        CurrentDocument = document;
        SelectDesignItem(null);
        ApplyValidationMessages(_validator.Validate(document));
        RequestPreviewRender(config, SelectedCanvas);
        LayoutSourceDisplay = I18nHelper.GetLocalizedString("LayoutSourceBuiltIn");
        LayoutSourcePath = _layoutService.GetBuiltInDefaultLayoutPath(windowTypeName, canvasName);
        StatusMessage = I18nHelper.GetLocalizedString("LayoutReset");
        ClearUndoRedo();
        RefreshDirtyState();
        return true;
    }

    [RelayCommand]
    private void OpenLayoutFolder()
    {
        if (SelectedCanvas is null)
        {
            return;
        }

        var folder = _layoutService.GetUserLayoutFolder(
            SelectedCanvas.WindowTypeName,
            SelectedCanvas.CanvasName);

        try
        {
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open fronted user layout folder: {Folder}", folder);
            StatusMessage = $"{I18nHelper.GetLocalizedString("OpenLayoutFolder")}: {ex.Message}";
        }
    }

    public void UpdateShiftSnapActive(bool isActive)
    {
        IsShiftSnapActive = isActive;
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

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        if (CurrentDocument is null || _undoStack.Count == 0)
        {
            StatusMessage = I18nHelper.GetLocalizedString("CannotUndo");
            return;
        }

        var total = StartDesignerPerfTrace();
        LogDesignerPerf("Undo", "start");
        var currentSnapshot = CreateSnapshot();
        LogDesignerPerf("Undo", "create current snapshot", Elapsed(total));
        if (currentSnapshot is not null)
        {
            _redoStack.Push(currentSnapshot);
        }

        RestoreSnapshot(_undoStack.Pop(), scheduleValidationAndPreview: true, traceOperation: "Undo");
        StatusMessage = I18nHelper.GetLocalizedString("Undo");
        LogDesignerPerf("Undo", "total", Elapsed(total));
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        if (CurrentDocument is null || _redoStack.Count == 0)
        {
            StatusMessage = I18nHelper.GetLocalizedString("CannotRedo");
            return;
        }

        var total = StartDesignerPerfTrace();
        LogDesignerPerf("Redo", "start");
        var currentSnapshot = CreateSnapshot();
        LogDesignerPerf("Redo", "create current snapshot", Elapsed(total));
        if (currentSnapshot is not null)
        {
            _undoStack.Push(currentSnapshot);
        }

        RestoreSnapshot(_redoStack.Pop(), scheduleValidationAndPreview: true, traceOperation: "Redo");
        StatusMessage = I18nHelper.GetLocalizedString("Redo");
        LogDesignerPerf("Redo", "total", Elapsed(total));
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

    [RelayCommand]
    private void SetZoomPreset(object? parameter)
    {
        var displayName = Convert.ToString(parameter, CultureInfo.InvariantCulture);
        var preset = ZoomPresets.FirstOrDefault(item =>
            string.Equals(item.DisplayName, displayName, StringComparison.OrdinalIgnoreCase));
        if (preset is not null)
        {
            ApplyZoomPreset(preset);
        }
    }

    [RelayCommand]
    private void AddControl(object? parameter)
    {
        if (CurrentDocument is null)
        {
            StatusMessage = I18nHelper.GetLocalizedString("CannotAddControl");
            return;
        }

        var request = parameter as FrontedAddControlRequest;
        var controlType = request?.ControlType ?? Convert.ToString(parameter, CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(controlType) || !_defaultConfigFactory.CanCreate(controlType))
        {
            StatusMessage = I18nHelper.GetLocalizedString("UnsupportedControlType");
            return;
        }

        if (CurrentDocument.Controls.Count >= FrontedLayoutLimits.MaxControlsPerCanvas)
        {
            StatusMessage = I18nHelper.GetLocalizedString("ControlCountLimitReached");
            return;
        }

        CaptureUndoSnapshot();
        var config = _defaultConfigFactory.Create(
            controlType,
            CurrentDocument,
            request?.CenterX,
            request?.CenterY);
        var item = new FrontedControlDesignItem
        {
            Name = _controlNameGenerator.Generate(GetNameSeed(controlType), CurrentDocument),
            Config = config,
            IsSelectableInEditor = true,
            IsEditableInEditor = true
        };

        item.IsRuntimeCritical = _runtimeContracts.IsRuntimeCritical(
            CurrentDocument.WindowTypeName,
            CurrentDocument.CanvasName,
            item.Name);

        CurrentDocument.Controls.Add(item);
        CurrentDocument.IsDirty = true;
        RefreshDirtyState();
        ControlFilterText = string.Empty;
        RebuildFilteredDesignItems();
        SelectDesignItem(item);
        ValidateCurrentDocument();
        RequestPreviewRenderCurrentDocument();
        StatusMessage = $"{I18nHelper.GetLocalizedString("AddedControl")}: {item.Name}";
    }

    [RelayCommand(CanExecute = nameof(CanCopySelectedControl))]
    private void CopySelectedControl()
    {
        if (!CanCopyControl(SelectedDesignItem))
        {
            StatusMessage = I18nHelper.GetLocalizedString("CannotCopyControl");
            return;
        }

        _copiedControl = FrontedDesignerClipboardPayload.Create(SelectedDesignItem!);
        PasteControlCommand.NotifyCanExecuteChanged();
        StatusMessage = I18nHelper.GetLocalizedString("CopyControl");
    }

    [RelayCommand(CanExecute = nameof(CanPasteControl))]
    private void PasteControl()
    {
        if (CurrentDocument is null || _copiedControl is null)
        {
            StatusMessage = I18nHelper.GetLocalizedString("CannotPasteControl");
            return;
        }

        if (CurrentDocument.Controls.Count >= FrontedLayoutLimits.MaxControlsPerCanvas)
        {
            StatusMessage = I18nHelper.GetLocalizedString("ControlCountLimitReached");
            return;
        }

        var total = StartDesignerPerfTrace();
        LogDesignerPerf("Paste", "start");
        var clonedConfig = _copiedControl.CreateConfig();
        LogDesignerPerf("Paste", "clone config", Elapsed(total));
        clonedConfig.Left += 10D;
        clonedConfig.Top += 10D;
        clonedConfig.ZIndex = CurrentDocument.Controls.Count == 0
            ? clonedConfig.ZIndex
            : CurrentDocument.Controls.Max(control => control.Config.ZIndex) + 1;

        var item = new FrontedControlDesignItem
        {
            Name = GeneratePasteName(_copiedControl.SourceName, _copiedControl.ControlType, CurrentDocument),
            Config = clonedConfig,
            IsSelectableInEditor = true,
            IsEditableInEditor = true
        };
        LogDesignerPerf("Paste", "name/z-index preparation", Elapsed(total));

        CaptureUndoSnapshot();
        LogDesignerPerf("Paste", "undo snapshot capture", Elapsed(total));
        CurrentDocument.Controls.Add(item);
        LogDesignerPerf("Paste", "add control", Elapsed(total));
        CurrentDocument.IsDirty = true;
        RefreshDirtyState();
        AddFilteredDesignItemIfVisible(item);
        LogDesignerPerf("Paste", "filtered list update", Elapsed(total));
        SelectDesignItem(item);
        LogDesignerPerf("Paste", "selection update", Elapsed(total));
        ScheduleValidationAndPreviewRender("Paste");
        LogDesignerPerf("Paste", "validation scheduling", Elapsed(total));
        LogDesignerPerf("Paste", "preview render scheduling", Elapsed(total));
        StatusMessage = $"{I18nHelper.GetLocalizedString("PasteControl")}: {item.Name}";
        LogDesignerPerf("Paste", "total", Elapsed(total));
    }

    [RelayCommand(CanExecute = nameof(CanDeleteSelectedControl))]
    private void DeleteSelectedControl()
    {
        if (CurrentDocument is null || SelectedDesignItem is null)
        {
            return;
        }

        var item = SelectedDesignItem;
        if (item.IsRuntimeCritical)
        {
            StatusMessage = I18nHelper.GetLocalizedString("CannotDeleteRuntimeCriticalControl");
            return;
        }

        if (!item.IsEditableInEditor || !item.IsSelectableInEditor)
        {
            StatusMessage = I18nHelper.GetLocalizedString("CannotDeleteRuntimeCriticalControl");
            return;
        }

        _referenceScanner.SetControls(CurrentDocument.Controls);
        if (_referenceScanner.GetIncomingReferences(item.Name).Count > 0)
        {
            StatusMessage = I18nHelper.GetLocalizedString("CannotDeleteReferencedControl");
            return;
        }

        var total = StartDesignerPerfTrace();
        LogDesignerPerf("Delete", "start");
        CaptureUndoSnapshot();
        LogDesignerPerf("Delete", "undo snapshot capture", Elapsed(total));
        CurrentDocument.Controls.Remove(item);
        LogDesignerPerf("Delete", "remove control", Elapsed(total));
        CurrentDocument.IsDirty = true;
        RefreshDirtyState();
        SelectDesignItem(null);
        RemoveFilteredDesignItem(item);
        LogDesignerPerf("Delete", "filtered list update", Elapsed(total));
        RebuildPropertyEditorItems();
        LogDesignerPerf("Delete", "selection/property update", Elapsed(total));
        ScheduleValidationAndPreviewRender("Delete");
        LogDesignerPerf("Delete", "validation scheduling", Elapsed(total));
        LogDesignerPerf("Delete", "preview render scheduling", Elapsed(total));
        StatusMessage = $"{I18nHelper.GetLocalizedString("DeleteSelectedControl")}: {item.Name}";
        LogDesignerPerf("Delete", "total", Elapsed(total));
    }

    [RelayCommand]
    private void ApplyCanvasSize()
    {
        ApplyCanvasSizeEdit(CanvasWidthEditText, CanvasHeightEditText);
    }

    public bool ApplyCanvasSizeEdit(string widthText, string heightText)
    {
        if (CurrentDocument is null)
        {
            return false;
        }

        if (!TryParsePositiveDouble(widthText, out var width)
            || !TryParsePositiveDouble(heightText, out var height))
        {
            CanvasPropertiesStatus = I18nHelper.GetLocalizedString("CanvasSizeMustBePositive");
            return false;
        }

        if (Math.Abs(CurrentDocument.CanvasConfig.CanvasWidth - width) < 0.0001D
            && Math.Abs(CurrentDocument.CanvasConfig.CanvasHeight - height) < 0.0001D)
        {
            RefreshCanvasPropertyBuffers();
            return true;
        }

        CaptureUndoSnapshot();
        CurrentDocument.CanvasConfig.CanvasWidth = width;
        CurrentDocument.CanvasConfig.CanvasHeight = height;
        CurrentDocument.IsDirty = true;
        RefreshCanvasPropertyBuffers();
        FinishCanvasConfigEdit(I18nHelper.GetLocalizedString("CanvasPropertiesApplied"));
        return true;
    }

    [RelayCommand]
    private void ApplyBackgroundImage()
    {
        ApplyCanvasBackgroundEdit(BackgroundImageEditText);
    }

    public bool ApplyCanvasBackgroundEdit(string? backgroundImage)
    {
        if (CurrentDocument is null)
        {
            return false;
        }

        var rawValue = string.IsNullOrWhiteSpace(backgroundImage) ? null : backgroundImage.Trim();
        var normalizedValue = rawValue is null
            ? null
            : FrontedTextLimitHelper.Clamp(rawValue, FrontedLayoutLimits.MaxResourcePathLength);
        if (!string.Equals(rawValue, normalizedValue, StringComparison.Ordinal))
        {
            CanvasPropertiesStatus = I18nHelper.GetLocalizedString("InputTruncated");
        }

        if (string.Equals(CurrentDocument.CanvasConfig.BackgroundImage, normalizedValue, StringComparison.Ordinal))
        {
            BackgroundImageEditText = normalizedValue ?? string.Empty;
            return true;
        }

        CaptureUndoSnapshot();
        CurrentDocument.CanvasConfig.BackgroundImage = normalizedValue;
        CurrentDocument.IsDirty = true;
        BackgroundImageEditText = normalizedValue ?? string.Empty;
        FinishCanvasConfigEdit(I18nHelper.GetLocalizedString("CanvasPropertiesApplied"));
        return true;
    }

    [RelayCommand]
    private void ClearBackgroundImage()
    {
        ClearCanvasBackground();
    }

    public bool ClearCanvasBackground()
    {
        return ApplyCanvasBackgroundEdit(null);
    }

    public bool StoreLocalBackgroundImage(string sourcePath)
    {
        if (_localResourceStore is null)
        {
            return false;
        }

        try
        {
            var uri = _localResourceStore.StoreImage(sourcePath);
            return ApplyCanvasBackgroundEdit(uri);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to store local fronted canvas background image.");
            CanvasPropertiesStatus = $"{I18nHelper.GetLocalizedString("FailedToApplyPicture")}: {ex.Message}";
            return false;
        }
    }

    [RelayCommand]
    private void ResetWindowOptions()
    {
        if (_windowLayoutOptionsService is null || SelectedWindow is null)
        {
            return;
        }

        _ = ResetWindowOptionsAsync();
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
        if (CurrentDocument is null || SelectedDesignItem is null || IsRebuildingPropertyGrid)
        {
            return;
        }

        FrontedDesignerGeometryHelper.Move(
            SelectedDesignItem,
            originalLeft,
            originalTop,
            deltaX,
            deltaY,
            CurrentDocument,
            EffectiveSnapEnabled,
            SnapGridSize);
        SyncLinkedOverlays(SelectedDesignItem);
        OnDesignItemGeometryChanged(renderPreview);
    }

    public void MoveSelectedDesignItemBy(double deltaX, double deltaY)
    {
        if (CurrentDocument is null || SelectedDesignItem is null)
        {
            return;
        }

        CaptureUndoSnapshot();
        FrontedDesignerGeometryHelper.MoveBy(
            SelectedDesignItem,
            deltaX,
            deltaY,
            CurrentDocument,
            EffectiveSnapEnabled,
            SnapGridSize);
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

        if (SelectedDesignItem.Config is BorderedImageFrontedControlConfig imageConfig
            && BorderedImageResizeTarget == FrontedDesignerResizeTarget.Image)
        {
            ResizeSelectedBorderedImageInnerImage(
                imageConfig,
                handle,
                originalWidth,
                originalHeight,
                deltaX,
                deltaY);
            CurrentDocument.IsDirty = true;
            OnDesignItemGeometryChanged(renderPreview);
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
            CurrentDocument,
            EffectiveSnapEnabled,
            SnapGridSize);
        SyncLinkedOverlays(SelectedDesignItem);
        OnDesignItemGeometryChanged(renderPreview);
    }

    private void ResizeSelectedBorderedImageInnerImage(
        BorderedImageFrontedControlConfig config,
        FrontedDesignerResizeHandleKind handle,
        double originalWidth,
        double originalHeight,
        double deltaX,
        double deltaY)
    {
        var widthDelta = handle is FrontedDesignerResizeHandleKind.Left
            or FrontedDesignerResizeHandleKind.TopLeft
            or FrontedDesignerResizeHandleKind.BottomLeft
            ? -deltaX
            : handle is FrontedDesignerResizeHandleKind.Right
                or FrontedDesignerResizeHandleKind.TopRight
                or FrontedDesignerResizeHandleKind.BottomRight
                ? deltaX
                : 0D;
        var heightDelta = handle is FrontedDesignerResizeHandleKind.Top
            or FrontedDesignerResizeHandleKind.TopLeft
            or FrontedDesignerResizeHandleKind.TopRight
            ? -deltaY
            : handle is FrontedDesignerResizeHandleKind.Bottom
                or FrontedDesignerResizeHandleKind.BottomLeft
                or FrontedDesignerResizeHandleKind.BottomRight
                ? deltaY
                : 0D;

        var width = originalWidth + widthDelta;
        var height = originalHeight + heightDelta;

        if (EffectiveSnapEnabled)
        {
            width = FrontedDesignerGeometryHelper.NormalizeCoordinate(width, effectiveSnapEnabled: true, SnapGridSize);
            height = FrontedDesignerGeometryHelper.NormalizeCoordinate(height, effectiveSnapEnabled: true, SnapGridSize);
        }
        else
        {
            width = FrontedDesignerGeometryHelper.Snap(width);
            height = FrontedDesignerGeometryHelper.Snap(height);
        }

        config.ImageWidth = Math.Max(FrontedDesignerGeometryHelper.MinResizeWidth, width);
        config.ImageHeight = Math.Max(FrontedDesignerGeometryHelper.MinResizeHeight, height);
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

    public bool IsLayerReorderable(FrontedControlDesignItem? item)
    {
        return item is
        {
            IsSelectableInEditor: true,
            IsEditableInEditor: true,
            IsLinkedOverlay: false
        } && item.Config is not PickingBorderOverlayControlConfig;
    }

    public bool CommitLayerDrop(
        FrontedControlDesignItem source,
        int? targetZIndex,
        FrontedControlDesignItem? targetItem,
        bool insertAfter,
        bool moveToNewTopLayer = false,
        bool moveToNewBottomLayer = false)
    {
        if (CurrentDocument is null || !CanReorderLayers || !IsLayerReorderable(source))
        {
            StatusMessage = I18nHelper.GetLocalizedString("Designer.LayerPanel.ReorderBlocked");
            return false;
        }

        if (targetItem is not null && !IsLayerReorderable(targetItem))
        {
            StatusMessage = I18nHelper.GetLocalizedString("Designer.LayerPanel.ReorderBlocked");
            return false;
        }

        var reorderableItems = CurrentDocument.Controls
            .Where(IsLayerReorderable)
            .ToList();
        if (!reorderableItems.Contains(source))
        {
            StatusMessage = I18nHelper.GetLocalizedString("Designer.LayerPanel.ReorderBlocked");
            return false;
        }

        var oldSnapshot = CreateSnapshot();
        var targetLayer = ResolveDropTargetZIndex(targetZIndex, moveToNewTopLayer, moveToNewBottomLayer);
        var desiredGroups = reorderableItems
            .Where(item => !ReferenceEquals(item, source))
            .GroupBy(item => item.Config.ZIndex)
            .ToDictionary(
                group => group.Key,
                group => group.ToList());

        if (!desiredGroups.TryGetValue(targetLayer, out var targetGroupItems))
        {
            targetGroupItems = [];
            desiredGroups[targetLayer] = targetGroupItems;
        }

        source.Config.ZIndex = targetLayer;
        var insertIndex = targetGroupItems.Count;
        if (targetItem is not null)
        {
            var targetIndex = targetGroupItems.IndexOf(targetItem);
            if (targetIndex >= 0)
            {
                insertIndex = targetIndex + (insertAfter ? 1 : 0);
            }
        }

        targetGroupItems.Insert(Math.Clamp(insertIndex, 0, targetGroupItems.Count), source);

        var desiredReorderable = desiredGroups
            .OrderByDescending(group => group.Key)
            .SelectMany(group => group.Value)
            .ToList();
        RebuildDocumentControlOrder(desiredReorderable);

        var newSnapshot = CreateSnapshot();
        if (oldSnapshot == newSnapshot)
        {
            RebuildFilteredDesignItems();
            SelectDesignItem(source);
            StatusMessage = I18nHelper.GetLocalizedString("Designer.LayerPanel.ReorderBlocked");
            return false;
        }

        if (oldSnapshot is not null)
        {
            if (!_undoStack.TryPeek(out var previous) || previous != oldSnapshot)
            {
                _undoStack.Push(oldSnapshot);
            }

            _redoStack.Clear();
            NotifyUndoRedoCommands();
        }

        CurrentDocument.IsDirty = true;
        RefreshDirtyState();
        RebuildFilteredDesignItems();
        SelectDesignItem(source);
        ScheduleValidationAndPreviewRender("LayerReorder");
        StatusMessage = I18nHelper.GetLocalizedString("Designer.LayerPanel.Reordered");
        return true;
    }

    public void CaptureUndoSnapshot()
    {
        var snapshot = CreateSnapshot();
        if (snapshot is null)
        {
            return;
        }

        if (_undoStack.TryPeek(out var previous) && previous == snapshot)
        {
            return;
        }

        _undoStack.Push(snapshot);
        _redoStack.Clear();
        NotifyUndoRedoCommands();
    }

    public void ClearPropertyEditErrorForBufferUpdate(string propertyName)
    {
        ClearPropertyEditError(propertyName);
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

    public bool ApplyPropertyEdit(FrontedPropertyEditorItem item, object? newValue)
    {
        if (CurrentDocument is null || SelectedDesignItem is null)
        {
            return false;
        }

        if (item.IsReadOnly || item.EditorKind == FrontedPropertyEditorKind.ReadOnly)
        {
            return false;
        }

        _propertyEditErrors.Remove(item.PropertyName);
        _propertyEditBuffers.Remove(item.PropertyName);

        if (item.PropertyName == nameof(FrontedControlDesignItem.Name))
        {
            return ApplyNameEdit(item, newValue);
        }

        var property = SelectedDesignItem.Config.GetType().GetProperty(
            item.PropertyName,
            BindingFlags.Instance | BindingFlags.Public);
        if (property is null || !property.CanWrite)
        {
            return false;
        }

        var commitValue = ClampEditorPropertyValue(item.PropertyName, SelectedDesignItem.Config.ControlType, newValue, out var wasClamped);
        if (!TryConvertPropertyValue(property, commitValue, out var convertedValue, out var errorMessage))
        {
            SetPropertyEditError(item, errorMessage, newValue);
            return false;
        }

        var oldValue = property.GetValue(SelectedDesignItem.Config);
        if (ValuesEqual(oldValue, convertedValue))
        {
            ClearPropertyEditError(item.PropertyName);
            item.Value = convertedValue;
            item.EditText = Convert.ToString(convertedValue, CultureInfo.InvariantCulture) ?? string.Empty;
            return true;
        }

        CaptureUndoSnapshot();
        property.SetValue(SelectedDesignItem.Config, convertedValue);
        item.Value = convertedValue;
        item.EditText = Convert.ToString(convertedValue, CultureInfo.InvariantCulture) ?? string.Empty;
        CurrentDocument.IsDirty = true;

        if (IsGeometryProperty(item.PropertyName))
        {
            SyncLinkedOverlays(SelectedDesignItem);
        }

        FinishPropertyEdit(item.PropertyName);
        if (wasClamped)
        {
            StatusMessage = I18nHelper.GetLocalizedString("InputTruncated");
        }

        return true;
    }

    private bool ApplyNameEdit(FrontedPropertyEditorItem item, object? newValue)
    {
        if (CurrentDocument is null || SelectedDesignItem is null)
        {
            return false;
        }

        if (SelectedDesignItem.IsRuntimeCritical
            || !SelectedDesignItem.IsSelectableInEditor
            || !SelectedDesignItem.IsEditableInEditor)
        {
            SetPropertyEditError(
                item,
                I18nHelper.GetLocalizedString("RuntimeCriticalControl"),
                newValue);
            return false;
        }

        var oldName = SelectedDesignItem.Name;
        var rawName = Convert.ToString(newValue, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
        var newName = FrontedTextLimitHelper.Clamp(rawName, FrontedLayoutLimits.MaxControlNameLength);
        var wasClamped = !string.Equals(rawName, newName, StringComparison.Ordinal);
        if (oldName == newName)
        {
            ClearPropertyEditError(item.PropertyName);
            item.Value = oldName;
            item.EditText = oldName;
            return true;
        }

        if (string.IsNullOrWhiteSpace(newName) || !ValidControlNameRegex.IsMatch(newName))
        {
            SetPropertyEditError(
                item,
                I18nHelper.GetLocalizedString("InvalidControlName"),
                newValue);
            return false;
        }

        if (CurrentDocument.Controls.Any(control =>
                !ReferenceEquals(control, SelectedDesignItem)
                && string.Equals(control.Name, newName, StringComparison.Ordinal)))
        {
            SetPropertyEditError(
                item,
                I18nHelper.GetLocalizedString("DuplicateControlName"),
                newValue);
            return false;
        }

        _referenceScanner.SetControls(CurrentDocument.Controls);
        if (_referenceScanner.GetIncomingReferences(oldName).Count > 0)
        {
            SetPropertyEditError(
                item,
                I18nHelper.GetLocalizedString("ReferencedControlRenameBlocked"),
                newValue);
            return false;
        }

        CaptureUndoSnapshot();
        SelectedDesignItem.Name = newName;
        item.Value = newName;
        item.EditText = newName;
        CurrentDocument.IsDirty = true;
        FinishPropertyEdit(item.PropertyName);
        if (wasClamped)
        {
            StatusMessage = I18nHelper.GetLocalizedString("InputTruncated");
        }

        return true;
    }

    private void ApplyLayoutSource(
        FrontedLayoutLoadResult loadResult,
        FrontedDesignerLayoutCatalogEntry entry)
    {
        LayoutSourceDisplay = loadResult.Source switch
        {
            FrontedLayoutSource.User => I18nHelper.GetLocalizedString("LayoutSourceUser"),
            FrontedLayoutSource.BuiltIn => I18nHelper.GetLocalizedString("LayoutSourceBuiltIn"),
            _ => I18nHelper.GetLocalizedString("LayoutSourceError")
        };
        LayoutSourcePath = loadResult.Path
            ?? _layoutService.GetBuiltInDefaultLayoutPath(entry.WindowTypeName, entry.CanvasName);

        if (!string.IsNullOrWhiteSpace(loadResult.Error))
        {
            StatusMessage = loadResult.Error;
        }
    }

    private async Task<FrontedCanvasConfig?> LoadBuiltInLayoutForResetAsync(
        string windowTypeName,
        string canvasName)
    {
        var loadResult = await _layoutService.LoadCanvasConfigWithMetadataAsync(windowTypeName, canvasName);
        return loadResult.Source == FrontedLayoutSource.BuiltIn ? loadResult.Config : null;
    }

    private void ClearLoadedLayout(FrontedLayoutValidationMessage message)
    {
        ControlFilterText = string.Empty;
        CurrentDocument = null;
        SelectDesignItem(null);
        ApplyValidationMessages([message]);
        RequestPreviewRender(null, SelectedCanvas);
    }

    private void ApplyValidationMessages(
        IReadOnlyList<FrontedLayoutValidationMessage> messages,
        bool refreshPropertyGrid = true)
    {
        _lastValidationMessages = messages;
        ValidationMessages.Clear();
        foreach (var message in messages.Take(FrontedLayoutLimits.MaxValidationMessagesShown))
        {
            ValidationMessages.Add(new FrontedLayoutValidationMessage
            {
                Severity = message.Severity,
                Code = message.Code,
                ControlName = message.ControlName,
                PropertyName = message.PropertyName,
                Message = FrontedTextLimitHelper.Clamp(
                    message.Message,
                    FrontedLayoutLimits.MaxValidationMessageLength)
            });
        }

        if (messages.Count > FrontedLayoutLimits.MaxValidationMessagesShown)
        {
            ValidationMessages.Add(CreateMessage(
                FrontedLayoutValidationSeverity.Info,
                "ValidationMessagesTruncated",
                string.Format(
                    CultureInfo.InvariantCulture,
                    I18nHelper.GetLocalizedString("ValidationMessagesTruncated"),
                    messages.Count - FrontedLayoutLimits.MaxValidationMessagesShown)));
        }

        ErrorCount = messages.Count(message => message.Severity == FrontedLayoutValidationSeverity.Error);
        WarningCount = messages.Count(message => message.Severity == FrontedLayoutValidationSeverity.Warning);
        InfoCount = messages.Count(message => message.Severity == FrontedLayoutValidationSeverity.Info);
        StatusMessage =
            $"{I18nHelper.GetLocalizedString("Errors")}: {ErrorCount}  "
            + $"{I18nHelper.GetLocalizedString("Warnings")}: {WarningCount}  "
            + $"{I18nHelper.GetLocalizedString("Infos")}: {InfoCount}";
        RefreshSelectedControlDisplay();
        if (refreshPropertyGrid)
        {
            RebuildPropertyEditorItems();
        }
    }

    private void ValidateCurrentDocument()
    {
        if (CurrentDocument is null || _validator is null)
        {
            return;
        }

        ApplyValidationMessages(_validator.Validate(CurrentDocument));
    }

    private void ScheduleValidationAndPreviewRender(string reason)
    {
        _scheduledValidationRequested = true;
        _scheduledPreviewRequested = true;
        if (_scheduledValidationAndPreviewPending)
        {
            LogDesignerPerf(reason, "validation/preview already scheduled");
            return;
        }

        _scheduledValidationAndPreviewPending = true;
        var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        dispatcher.BeginInvoke(
            new Action(ExecuteScheduledValidationAndPreviewRender),
            DispatcherPriority.Background);
    }

    public void ExecuteScheduledDesignerWorkForTests()
    {
        if (!_scheduledValidationAndPreviewPending)
        {
            return;
        }

        ExecuteScheduledValidationAndPreviewRender();
    }

    private void ExecuteScheduledValidationAndPreviewRender()
    {
        if (!_scheduledValidationAndPreviewPending)
        {
            return;
        }

        _scheduledValidationAndPreviewPending = false;
        var shouldValidate = _scheduledValidationRequested;
        var shouldPreview = _scheduledPreviewRequested;
        _scheduledValidationRequested = false;
        _scheduledPreviewRequested = false;

        if (CurrentDocument is null)
        {
            return;
        }

        var total = StartDesignerPerfTrace();
        if (shouldValidate)
        {
            ValidateCurrentDocument();
            ScheduledDesignerValidationExecutionCount++;
            LogDesignerPerf("ScheduledDesignerWork", "validation execution", Elapsed(total));
        }

        if (shouldPreview)
        {
            RequestPreviewRenderCurrentDocument();
            ScheduledDesignerPreviewExecutionCount++;
            LogDesignerPerf("ScheduledDesignerWork", "preview render execution", Elapsed(total));
        }
    }

    private void RebuildPropertyEditorItems()
    {
        _isRebuildingPropertyGrid = true;
        OnPropertyChanged(nameof(IsRebuildingPropertyGrid));
        try
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
                if (_propertyEditErrors.TryGetValue(row.PropertyName, out var editError))
                {
                    if (_propertyEditBuffers.TryGetValue(row.PropertyName, out var editBuffer))
                    {
                        row.EditText = editBuffer;
                    }

                    row.SetEditError(editError);
                    row.ValidationErrors = row.ValidationErrors
                        .Concat([editError])
                        .Distinct(StringComparer.Ordinal)
                        .ToArray();
                }

                PropertyEditorItems.Add(row);
            }
        }
        finally
        {
            _isRebuildingPropertyGrid = false;
            OnPropertyChanged(nameof(IsRebuildingPropertyGrid));
        }
    }

    private void SetPropertyEditError(FrontedPropertyEditorItem item, string message, object? attemptedValue)
    {
        var attemptedText = Convert.ToString(attemptedValue, CultureInfo.InvariantCulture) ?? string.Empty;
        _propertyEditErrors[item.PropertyName] = message;
        _propertyEditBuffers[item.PropertyName] = attemptedText;
        item.EditText = attemptedText;
        item.SetEditError(message);
        item.ValidationErrors = item.ValidationErrors
            .Concat([message])
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        StatusMessage = message;
    }

    private void ClearPropertyEditError(string propertyName)
    {
        _propertyEditErrors.Remove(propertyName);
        _propertyEditBuffers.Remove(propertyName);
    }

    private void FinishPropertyEdit(string propertyName)
    {
        ClearPropertyEditError(propertyName);
        RefreshDirtyState();
        RebuildFilteredDesignItems();
        ValidateCurrentDocument();
        RequestPreviewRenderCurrentDocument();
        DeleteSelectedControlCommand.NotifyCanExecuteChanged();
    }

    private void FinishCanvasConfigEdit(string statusMessage)
    {
        CanvasPropertiesStatus = statusMessage;
        RefreshDirtyState();
        ValidateCurrentDocument();
        RequestPreviewRenderCurrentDocument();
        UpdateFitZoomFromCurrentDocument();
    }

    private void RefreshCanvasPropertyBuffers()
    {
        if (CurrentDocument is null)
        {
            CanvasWidthEditText = string.Empty;
            CanvasHeightEditText = string.Empty;
            BackgroundImageEditText = string.Empty;
            return;
        }

        CanvasWidthEditText = CurrentDocument.CanvasConfig.CanvasWidth.ToString("0.##", CultureInfo.InvariantCulture);
        CanvasHeightEditText = CurrentDocument.CanvasConfig.CanvasHeight.ToString("0.##", CultureInfo.InvariantCulture);
        BackgroundImageEditText = CurrentDocument.CanvasConfig.BackgroundImage ?? string.Empty;
    }

    private void LoadWindowOptions(string windowTypeName)
    {
        WindowOptionsWindowTypeName = $"{_localizationService.GetWindowDisplayName(windowTypeName)} ({windowTypeName})";
        WindowOptionsRestartRequired = false;

        if (_windowLayoutOptionsService is null)
        {
            WindowAllowTransparency = false;
            return;
        }

        _isLoadingWindowOptions = true;
        try
        {
            var options = _windowLayoutOptionsService.LoadOptions(windowTypeName);
            WindowAllowTransparency = options.AllowTransparency;
            WindowOptionsStatus = string.Empty;
        }
        finally
        {
            _isLoadingWindowOptions = false;
        }
    }

    private async Task SaveWindowOptionsAsync(bool allowTransparency)
    {
        if (_windowLayoutOptionsService is null || SelectedWindow is null)
        {
            return;
        }

        try
        {
            await _windowLayoutOptionsService.SaveOptionsAsync(
                SelectedWindow.WindowTypeName,
                new FrontedWindowLayoutOptions
                {
                    AllowTransparency = allowTransparency
                });
            WindowOptionsRestartRequired = true;
            WindowOptionsStatus = I18nHelper.GetLocalizedString("RestartRequired");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save fronted window layout options.");
            WindowOptionsStatus = ex.Message;
        }
    }

    private async Task ResetWindowOptionsAsync()
    {
        if (_windowLayoutOptionsService is null || SelectedWindow is null)
        {
            return;
        }

        try
        {
            await _windowLayoutOptionsService.ResetOptionsAsync(SelectedWindow.WindowTypeName);
            LoadWindowOptions(SelectedWindow.WindowTypeName);
            WindowOptionsRestartRequired = true;
            WindowOptionsStatus = I18nHelper.GetLocalizedString("RestartRequired");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to reset fronted window layout options.");
            WindowOptionsStatus = ex.Message;
        }
    }

    private void OnDesignItemGeometryChanged(bool renderPreview)
    {
        RefreshDirtyState();
        RefreshSelectedControlDisplay();

        if (renderPreview)
        {
            ValidateCurrentDocument();
            RequestPreviewRenderCurrentDocument();
        }
    }

    private void RefreshDirtyState()
    {
        DirtyIndicatorText = CurrentDocument?.IsDirty == true
            ? $"* {I18nHelper.GetLocalizedString("Unsaved")}"
            : string.Empty;
        NotifyLayoutCommandState();
    }

    private void NotifyLayoutCommandState()
    {
        OnPropertyChanged(nameof(CanSaveLayout));
        OnPropertyChanged(nameof(CanResetToBuiltIn));
        SaveLayoutCommand.NotifyCanExecuteChanged();
        ResetToBuiltInCommand.NotifyCanExecuteChanged();
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

    private int ResolveDropTargetZIndex(
        int? targetZIndex,
        bool moveToNewTopLayer,
        bool moveToNewBottomLayer)
    {
        if (CurrentDocument is null || CurrentDocument.Controls.Count == 0)
        {
            return targetZIndex ?? 0;
        }

        if (moveToNewTopLayer)
        {
            return CurrentDocument.Controls.Max(control => control.Config.ZIndex) + 1;
        }

        if (moveToNewBottomLayer)
        {
            return CurrentDocument.Controls.Min(control => control.Config.ZIndex) - 1;
        }

        return targetZIndex ?? 0;
    }

    private void RebuildDocumentControlOrder(IReadOnlyList<FrontedControlDesignItem> desiredReorderable)
    {
        if (CurrentDocument is null)
        {
            return;
        }

        var originalControls = CurrentDocument.Controls.ToList();
        var added = new HashSet<FrontedControlDesignItem>();
        var rebuilt = new List<FrontedControlDesignItem>(originalControls.Count);

        foreach (var item in desiredReorderable)
        {
            AddControlAndLinkedOverlays(item, originalControls, rebuilt, added);
        }

        foreach (var item in originalControls)
        {
            if (added.Contains(item))
            {
                continue;
            }

            rebuilt.Add(item);
            added.Add(item);
        }

        CurrentDocument.Controls.Clear();
        foreach (var item in rebuilt)
        {
            CurrentDocument.Controls.Add(item);
        }
    }

    private static void AddControlAndLinkedOverlays(
        FrontedControlDesignItem item,
        IReadOnlyList<FrontedControlDesignItem> originalControls,
        ICollection<FrontedControlDesignItem> rebuilt,
        ISet<FrontedControlDesignItem> added)
    {
        if (!added.Add(item))
        {
            return;
        }

        rebuilt.Add(item);
        foreach (var overlay in originalControls.Where(control =>
                     control.Config is PickingBorderOverlayControlConfig overlayConfig
                     && string.Equals(overlayConfig.TargetControlName, item.Name, StringComparison.Ordinal)))
        {
            overlay.Config.ZIndex = item.Config.ZIndex;
            if (added.Add(overlay))
            {
                rebuilt.Add(overlay);
            }
        }
    }

    private void RebuildFilteredDesignItems()
    {
        FilteredDesignItems.Clear();
        LayerGroups.Clear();

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

        RebuildLayerGroups();
    }

    private void AddFilteredDesignItemIfVisible(FrontedControlDesignItem item)
    {
        var filter = ControlFilterText?.Trim();
        if (!item.IsSelectableInEditor || !MatchesControlFilter(item, filter))
        {
            return;
        }

        var insertIndex = GetFilteredInsertIndex(item);
        FilteredDesignItems.Insert(insertIndex, item);
        RebuildLayerGroups();
    }

    private void RemoveFilteredDesignItem(FrontedControlDesignItem item)
    {
        var index = FilteredDesignItems.IndexOf(item);
        if (index >= 0)
        {
            FilteredDesignItems.RemoveAt(index);
            RebuildLayerGroups();
        }
    }

    private void RefreshFilteredDesignItemPosition(FrontedControlDesignItem item)
    {
        RemoveFilteredDesignItem(item);
        AddFilteredDesignItemIfVisible(item);
    }

    private void RebuildLayerGroups()
    {
        LayerGroups.Clear();
        foreach (var group in FilteredDesignItems
                     .GroupBy(item => item.Config.ZIndex)
                     .OrderByDescending(group => group.Key))
        {
            var layerGroup = new FrontedLayerGroup
            {
                ZIndex = group.Key,
                DisplayName = $"{I18nHelper.GetLocalizedString("Designer.LayerPanel.Layer")} {group.Key}"
            };

            foreach (var item in group.OrderBy(item => CurrentDocument?.Controls.IndexOf(item) ?? 0))
            {
                layerGroup.Items.Add(item);
            }

            LayerGroups.Add(layerGroup);
        }
    }

    private int GetFilteredInsertIndex(FrontedControlDesignItem item)
    {
        if (CurrentDocument is null)
        {
            return FilteredDesignItems.Count;
        }

        for (var index = 0; index < FilteredDesignItems.Count; index++)
        {
            if (CompareFilteredOrder(item, FilteredDesignItems[index]) < 0)
            {
                return index;
            }
        }

        return FilteredDesignItems.Count;
    }

    private int CompareFilteredOrder(FrontedControlDesignItem left, FrontedControlDesignItem right)
    {
        var zIndexCompare = right.Config.ZIndex.CompareTo(left.Config.ZIndex);
        if (zIndexCompare != 0)
        {
            return zIndexCompare;
        }

        if (CurrentDocument is null)
        {
            return 0;
        }

        return CurrentDocument.Controls.IndexOf(left).CompareTo(CurrentDocument.Controls.IndexOf(right));
    }

    private void NormalizeSelectionState()
    {
        if (_lastSelectedDesignItem is not null)
        {
            _lastSelectedDesignItem.IsSelected = false;
        }

        _lastSelectedDesignItem = null;
        if (CurrentDocument is null)
        {
            return;
        }

        foreach (var control in CurrentDocument.Controls)
        {
            control.IsSelected = ReferenceEquals(control, SelectedDesignItem);
            if (control.IsSelected)
            {
                _lastSelectedDesignItem = control;
            }
        }
    }

    private Stopwatch? StartDesignerPerfTrace()
    {
        return _logger.IsEnabled(LogLevel.Debug) ? Stopwatch.StartNew() : null;
    }

    private static TimeSpan Elapsed(Stopwatch? stopwatch)
    {
        return stopwatch?.Elapsed ?? TimeSpan.Zero;
    }

    [Conditional("DEBUG")]
    private void LogDesignerPerf(string operation, string stage)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("FrontedDesigner perf {Operation}: {Stage}", operation, stage);
        }
    }

    [Conditional("DEBUG")]
    private void LogDesignerPerf(string operation, string stage, TimeSpan elapsed)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "FrontedDesigner perf {Operation}: {Stage} at {ElapsedMilliseconds:F2} ms",
                operation,
                stage,
                elapsed.TotalMilliseconds);
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
        SelectedControlTypeDisplay = _localizationService.GetControlTypeDisplayName(config.ControlType);
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
                        CanvasName = entry.CanvasName,
                        SharedDataServiceOverride = _designerPreviewSharedDataService,
                        RenderMissingPluginPlaceholders = true
                    }));
    }

    private void RebuildAddControlCatalog()
    {
        AddControlCatalogGroups.Clear();
        foreach (var group in _defaultConfigFactory.GetCatalog())
        {
            AddControlCatalogGroups.Add(group);
        }
    }

    private string? CreateSnapshot()
    {
        if (CurrentDocument is null)
        {
            return null;
        }

        return JsonSerializer.Serialize(_designConverter.ToConfig(CurrentDocument));
    }

    private static bool CanCopyControl(FrontedControlDesignItem? item)
    {
        return item is
        {
            IsSelectableInEditor: true,
            IsEditableInEditor: true,
            IsRuntimeCritical: false
        } && item.Config is not PickingBorderOverlayControlConfig;
    }

    private static string GeneratePasteName(string sourceName, string controlType, FrontedCanvasDesignDocument document)
    {
        var match = Regex.Match(sourceName, "^(.*?)(\\d+)$", RegexOptions.CultureInvariant);
        var baseName = match.Success ? match.Groups[1].Value : GetNameSeed(controlType);
        var index = match.Success && int.TryParse(match.Groups[2].Value, out var parsed) ? parsed + 1 : 1;
        var existingNames = document.Controls.Select(control => control.Name).ToHashSet(StringComparer.Ordinal);

        while (true)
        {
            var candidate = FrontedTextLimitHelper.Clamp($"{baseName}{index}", FrontedLayoutLimits.MaxControlNameLength);
            if (!existingNames.Contains(candidate) && ValidControlNameRegex.IsMatch(candidate))
            {
                return candidate;
            }

            index++;
        }
    }

    private static string GetNameSeed(string controlType)
    {
        return FrontedPluginControlType.TryParse(controlType, out var parsed)
            ? parsed.ControlTypeName
            : controlType;
    }

    private static object? ClampEditorPropertyValue(
        string propertyName,
        string? controlType,
        object? newValue,
        out bool wasClamped)
    {
        wasClamped = false;
        if (newValue is not string text)
        {
            return newValue;
        }

        var maxLength = FrontedTextLimitHelper.GetMaxLengthForProperty(propertyName, controlType);
        if (maxLength == int.MaxValue)
        {
            return newValue;
        }

        var clamped = FrontedTextLimitHelper.Clamp(text, maxLength);
        wasClamped = !string.Equals(text, clamped, StringComparison.Ordinal);
        return clamped;
    }

    private void RestoreSnapshot(
        string snapshot,
        bool scheduleValidationAndPreview = false,
        string traceOperation = "RestoreSnapshot")
    {
        if (CurrentDocument is null)
        {
            return;
        }

        var total = StartDesignerPerfTrace();
        var selectedName = SelectedDesignItem?.Name;
        var windowTypeName = CurrentDocument.WindowTypeName;
        var canvasName = CurrentDocument.CanvasName;
        var config = JsonSerializer.Deserialize<FrontedCanvasConfig>(snapshot);
        LogDesignerPerf(traceOperation, "restore snapshot deserialize", Elapsed(total));
        if (config is null)
        {
            return;
        }

        _isRestoringSnapshot = true;
        try
        {
            var document = _designConverter.FromConfig(
                windowTypeName,
                canvasName,
                config,
                _runtimeContracts);
            LogDesignerPerf(traceOperation, "design document rebuild", Elapsed(total));
            document.IsDirty = true;
            CurrentDocument = document;
            SelectDesignItem(document.Controls.FirstOrDefault(control =>
                string.Equals(control.Name, selectedName, StringComparison.Ordinal)));
            NormalizeSelectionState();
            if (scheduleValidationAndPreview)
            {
                ScheduleValidationAndPreviewRender(traceOperation);
                LogDesignerPerf(traceOperation, "validation scheduling", Elapsed(total));
                LogDesignerPerf(traceOperation, "preview render scheduling", Elapsed(total));
            }
            else
            {
                ApplyValidationMessages(_validator.Validate(document));
                LogDesignerPerf(traceOperation, "validation execution", Elapsed(total));
                RequestPreviewRender(config, SelectedCanvas);
                LogDesignerPerf(traceOperation, "preview render execution", Elapsed(total));
            }

            RefreshDirtyState();
            LogDesignerPerf(traceOperation, "total", Elapsed(total));
        }
        finally
        {
            _isRestoringSnapshot = false;
            NotifyUndoRedoCommands();
        }
    }

    private void ClearUndoRedo()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        NotifyUndoRedoCommands();
    }

    private void NotifyUndoRedoCommands()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private bool HasIncomingReferences(string controlName)
    {
        if (CurrentDocument is null)
        {
            return false;
        }

        _referenceScanner.SetControls(CurrentDocument.Controls);
        return _referenceScanner.GetIncomingReferences(controlName).Count > 0;
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

    public void ApplyManualZoom(double scale)
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

    public bool TryApplyZoomText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            UpdateZoomEditTextFromCurrentZoom();
            return false;
        }

        text = text.Trim();

        var fitKey = I18nHelper.GetLocalizedString("Fit");
        if (string.Equals(text, fitKey, StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "Fit", StringComparison.OrdinalIgnoreCase))
        {
            FitToWindow();
            return true;
        }

        double scale;
        if (text.EndsWith("x", StringComparison.OrdinalIgnoreCase))
        {
            var numericPart = text.AsSpan(0, text.Length - 1).Trim();
            if (double.TryParse(numericPart, NumberStyles.Float, CultureInfo.InvariantCulture, out var multiplier) && multiplier > 0D)
            {
                scale = multiplier;
            }
            else
            {
                StatusMessage = I18nHelper.GetLocalizedString("Designer.ZoomInvalid");
                UpdateZoomEditTextFromCurrentZoom();
                return false;
            }
        }
        else if (text.EndsWith("%", StringComparison.Ordinal))
        {
            var numericPart = text.AsSpan(0, text.Length - 1).Trim();
            if (double.TryParse(numericPart, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent) && percent > 0D)
            {
                scale = percent / 100D;
            }
            else
            {
                StatusMessage = I18nHelper.GetLocalizedString("Designer.ZoomInvalid");
                UpdateZoomEditTextFromCurrentZoom();
                return false;
            }
        }
        else if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var plainNumber) && plainNumber > 0D)
        {
            scale = plainNumber / 100D;
        }
        else
        {
            StatusMessage = I18nHelper.GetLocalizedString("Designer.ZoomInvalid");
            UpdateZoomEditTextFromCurrentZoom();
            return false;
        }

        ApplyManualZoom(scale);
        return true;
    }

    public void ApplyZoomPercent(double percent)
    {
        ZoomPercent = percent;
    }

    private void UpdateZoomEditTextFromCurrentZoom()
    {
        if (IsFitMode)
        {
            ZoomEditText = I18nHelper.GetLocalizedString("Fit");
        }
        else
        {
            ZoomEditText = $"{ZoomScale:P0}";
        }
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
                if (IsColorProperty(property.Name))
                {
                    if (!FrontedPropertyColorHelper.TryParseArgbColor(text, out var color))
                    {
                        errorMessage = I18nHelper.GetLocalizedString("PropertyValidationErrors");
                        return false;
                    }

                    convertedValue = FrontedPropertyColorHelper.ToArgbString(color);
                }
                else
                {
                    convertedValue = text;
                }
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
            or nameof(FrontedControlConfigBase.Height)
            or nameof(BorderedImageFrontedControlConfig.ImageWidth)
            or nameof(BorderedImageFrontedControlConfig.ImageHeight))
        {
            return Math.Max(
                FrontedDesignerGeometryHelper.MinResizeWidth,
                FrontedDesignerGeometryHelper.Snap(value));
        }

        return value;
    }

    private static bool TryParsePositiveDouble(string text, out double value)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
               && double.IsFinite(value)
               && value > 0D;
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

    private static bool IsColorProperty(string propertyName)
    {
        return propertyName.EndsWith("Color", StringComparison.OrdinalIgnoreCase)
               || propertyName.Equals("Foreground", StringComparison.OrdinalIgnoreCase)
               || propertyName.Equals("Background", StringComparison.OrdinalIgnoreCase);
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

public sealed class FrontedDesignerClipboardPayload(
    string sourceName,
    string controlType,
    string configJson,
    Type configType)
{
    public string SourceName { get; } = sourceName;

    public string ControlType { get; } = controlType;

    public string ConfigJson { get; } = configJson;

    public Type ConfigType { get; } = configType;

    public static FrontedDesignerClipboardPayload Create(FrontedControlDesignItem item)
    {
        var configType = item.Config.GetType();
        return new FrontedDesignerClipboardPayload(
            item.Name,
            item.Config.ControlType,
            JsonSerializer.Serialize(item.Config, configType),
            configType);
    }

    public FrontedControlConfigBase CreateConfig()
    {
        return (FrontedControlConfigBase?)JsonSerializer.Deserialize(ConfigJson, ConfigType)
               ?? throw new InvalidOperationException("Failed to deserialize copied control config.");
    }
}

public sealed class FrontedDesignerPreviewRenderRequestedEventArgs(
    FrontedCanvasConfig? config,
    FrontedRenderContext? context) : EventArgs
{
    public FrontedCanvasConfig? Config { get; } = config;

    public FrontedRenderContext? Context { get; } = context;
}
