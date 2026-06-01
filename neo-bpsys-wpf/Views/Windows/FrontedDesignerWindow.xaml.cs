using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.ViewModels.Windows;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Wpf.Ui.Controls;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>
/// Interaction logic for FrontedDesignerWindow.xaml.
/// </summary>
public partial class FrontedDesignerWindow : FluentWindow
{
    private readonly IFrontedRenderer? _renderer;
    private readonly IFilePickerService? _filePickerService;
    private readonly FrontedBindingBrowserProvider? _bindingBrowserProvider;
    private readonly FrontedResourceBrowserProvider? _resourceBrowserProvider;
    private readonly ILogger<FrontedDesignerWindow>? _logger;
    private bool _isLoaded;
    private bool _suppressPropertyEditorCommit;
    private FrontedDesignerWindowViewModel? _viewModel;
    private ValidationDetailsWindow? _validationDetailsWindow;
    private readonly Dictionary<FrontedControlDesignItem, Border> _hitboxes = new();
    private readonly Dictionary<FrontedDesignerResizeHandleKind, Border> _resizeHandles = new();
    private Border? _selectionOutline;
    private Border? _selectionLabel;
    private FrameworkElement? _capturedElement;
    private InteractionMode _interactionMode = InteractionMode.None;
    private FrontedDesignerResizeHandleKind? _activeResizeHandle;
    private FrontedControlDesignItem? _pendingHitCandidate;
    private bool _isPendingEmptyClick;
    private bool _hasExceededClickThreshold;
    private bool _hasStartedDrag;
    private Point _startMousePosition;
    private double _originalLeft;
    private double _originalTop;
    private double _originalWidth;
    private double _originalHeight;
    private bool _isPanningViewport;
    private Point _panStartViewportPosition;
    private double _panStartHorizontalOffset;
    private double _panStartVerticalOffset;
    private Cursor? _cursorBeforePan;
    private bool _selectorReloadScheduled;
    private bool _suppressSelectorReload;
    private bool _forceCloseAfterDirtyPrompt;
    private bool _isDirtyClosePromptOpen;
    private FrontedDesignerWindowOption? _lastAcceptedWindow;
    private FrontedDesignerLayoutCatalogEntry? _lastAcceptedCanvas;

    public FrontedDesignerWindow()
    {
        InitializeComponent();
    }

    public FrontedDesignerWindow(
        FrontedDesignerWindowViewModel viewModel,
        IFrontedRenderer renderer,
        IFilePickerService filePickerService,
        FrontedBindingBrowserProvider bindingBrowserProvider,
        FrontedResourceBrowserProvider resourceBrowserProvider,
        ILogger<FrontedDesignerWindow> logger)
    {
        _renderer = renderer;
        _filePickerService = filePickerService;
        _bindingBrowserProvider = bindingBrowserProvider;
        _resourceBrowserProvider = resourceBrowserProvider;
        _logger = logger;

        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
        Closed += OnClosed;
        Closing += OnClosing;
        Deactivated += OnDeactivated;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        AttachViewModel();
        _ = LoadInitialLayoutAsync();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PreviewRenderRequested -= OnPreviewRenderRequested;
            _viewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
            _viewModel.PropertyEditorItems.CollectionChanged -= PropertyEditorItems_OnCollectionChanged;
        }

        CloseValidationDetailsWindowSafely();
        _validationDetailsWindow = null;
    }

    private async Task LoadInitialLayoutAsync()
    {
        if (_viewModel is null)
        {
            return;
        }

        await _viewModel.ReloadLayoutCoreAsync();
        _lastAcceptedWindow = _viewModel.SelectedWindow;
        _lastAcceptedCanvas = _viewModel.SelectedCanvas;
    }

    private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded || _suppressSelectorReload)
        {
            return;
        }

        ScheduleSelectorReload();
    }

    private void ScheduleSelectorReload()
    {
        if (_selectorReloadScheduled)
        {
            return;
        }

        _selectorReloadScheduled = true;
        Dispatcher.BeginInvoke(
            new Action(async () => await HandleScheduledSelectorReloadAsync()),
            DispatcherPriority.Background);
    }

    private async Task HandleScheduledSelectorReloadAsync()
    {
        _selectorReloadScheduled = false;
        if (_viewModel is null)
        {
            return;
        }

        if (ReferenceEquals(_lastAcceptedWindow, _viewModel.SelectedWindow)
            && ReferenceEquals(_lastAcceptedCanvas, _viewModel.SelectedCanvas))
        {
            return;
        }

        if (!await ConfirmDirtyDocumentCanContinueAsync("SaveBeforeSwitch"))
        {
            RestoreAcceptedSelection();
            return;
        }

        await _viewModel.ReloadLayoutCoreAsync();
        _lastAcceptedWindow = _viewModel.SelectedWindow;
        _lastAcceptedCanvas = _viewModel.SelectedCanvas;
    }

    private void RestoreAcceptedSelection()
    {
        if (_viewModel is null)
        {
            return;
        }

        _suppressSelectorReload = true;
        try
        {
            _viewModel.SelectedWindow = _lastAcceptedWindow;
            _viewModel.SelectedCanvas = _lastAcceptedCanvas;
        }
        finally
        {
            _suppressSelectorReload = false;
        }
    }

    private void ControlList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0 || _viewModel is null)
        {
            return;
        }

        if (e.AddedItems[0] is FrontedControlDesignItem item)
        {
            _viewModel.SelectDesignItem(item);
        }
    }

    private void ControlListItem_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: FrontedControlDesignItem item })
        {
            _viewModel?.SelectDesignItem(item);
        }
    }

    private void Window_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        UpdateShiftSnapState();

        if (_viewModel is null || ShouldIgnoreKeyboardInput())
        {
            return;
        }

        var isControl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var isShift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        if (!isControl)
        {
            return;
        }

        if (e.Key == Key.S && !isShift)
        {
            if (_viewModel.CanSaveLayout)
            {
                _viewModel.SaveLayoutCommand.Execute(null);
            }

            e.Handled = true;
        }
        else if (e.Key == Key.Z && !isShift)
        {
            _viewModel.UndoCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Y || (e.Key == Key.Z && isShift))
        {
            _viewModel.RedoCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.C && !isShift)
        {
            _viewModel.CopySelectedControlCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.V && !isShift)
        {
            _viewModel.PasteControlCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void PropertyTextBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        var committed = ApplyPropertyEditorValue(sender);
        if (committed)
        {
            FocusDesignSurface();
        }

        e.Handled = true;
    }

    private void PropertyTextApplyButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (ApplyPropertyEditorValue(sender))
        {
            FocusDesignSurface();
        }
    }

    private void Window_OnPreviewKeyUp(object sender, KeyEventArgs e)
    {
        UpdateShiftSnapState();
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        _viewModel?.UpdateShiftSnapActive(false);
    }

    private void BrowseBindingButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_bindingBrowserProvider is null
            || sender is not FrameworkElement { DataContext: FrontedPropertyEditorItem item })
        {
            return;
        }

        var viewModel = new FrontedBindingBrowserWindowViewModel(
            _bindingBrowserProvider,
            new FrontedBindingTypeFilter(item.BindingTargetKind));
        var window = new FrontedBindingBrowserWindow
        {
            Owner = this,
            DataContext = viewModel
        };
        window.InitializeSelection(item.EditText);

        if (window.ShowDialog() == true && !string.IsNullOrWhiteSpace(window.SelectedBindingPath))
        {
            item.EditText = window.SelectedBindingPath;
            _viewModel?.ClearPropertyEditErrorForBufferUpdate(item.PropertyName);
        }
    }

    private void BrowseResourceButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_resourceBrowserProvider is null
            || sender is not FrameworkElement { DataContext: FrontedPropertyEditorItem item })
        {
            return;
        }

        var viewModel = new FrontedResourceBrowserWindowViewModel(_resourceBrowserProvider);
        var window = new FrontedResourceBrowserWindow
        {
            Owner = this,
            DataContext = viewModel
        };
        window.InitializeSelection(item.EditText);

        if (window.ShowDialog() == true && !string.IsNullOrWhiteSpace(window.SelectedResourcePath))
        {
            item.EditText = window.SelectedResourcePath;
            _viewModel?.ClearPropertyEditErrorForBufferUpdate(item.PropertyName);
        }
    }

    private void BrowseCanvasBackgroundResourceButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_resourceBrowserProvider is null || _viewModel is null)
        {
            return;
        }

        var viewModel = new FrontedResourceBrowserWindowViewModel(_resourceBrowserProvider);
        var window = new FrontedResourceBrowserWindow
        {
            Owner = this,
            DataContext = viewModel
        };
        window.InitializeSelection(_viewModel.BackgroundImageEditText);

        if (window.ShowDialog() == true && !string.IsNullOrWhiteSpace(window.SelectedResourcePath))
        {
            _viewModel.BackgroundImageEditText = window.SelectedResourcePath;
        }
    }

    private void ChooseLocalCanvasBackgroundButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_filePickerService is null || _viewModel is null)
        {
            return;
        }

        var file = _filePickerService.PickImage();
        if (!string.IsNullOrWhiteSpace(file))
        {
            _viewModel.StoreLocalBackgroundImage(file);
        }
    }

    private void PropertyCheckBox_OnClick(object sender, RoutedEventArgs e)
    {
        ApplyPropertyEditorValue(sender);
    }

    private void PropertyComboBox_OnDropDownClosed(object sender, EventArgs e)
    {
        if (sender is not ComboBox comboBox || !comboBox.IsKeyboardFocusWithin)
        {
            return;
        }

        ApplyPropertyEditorValue(sender);
    }

    private void PropertyFontComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox
            || e.AddedItems.Count == 0
            || !comboBox.IsDropDownOpen
            || !comboBox.IsKeyboardFocusWithin)
        {
            return;
        }

        ApplyFontComboBoxValue(comboBox, useSelectedOption: true);
    }

    private void PropertyFontComboBox_OnDropDownClosed(object sender, EventArgs e)
    {
        if (sender is not ComboBox comboBox || !comboBox.IsKeyboardFocusWithin)
        {
            return;
        }

        ApplyFontComboBoxValue(comboBox, useSelectedOption: true);
    }

    private void PropertyFontComboBox_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && !comboBox.IsDropDownOpen)
        {
            ApplyFontComboBoxValue(comboBox, useSelectedOption: false);
        }
    }

    private void PropertyFontComboBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not ComboBox comboBox)
        {
            return;
        }

        var committed = ApplyFontComboBoxValue(comboBox, useSelectedOption: false);
        if (committed)
        {
            FocusDesignSurface();
        }

        e.Handled = true;
    }

    private bool ApplyFontComboBoxValue(ComboBox comboBox, bool useSelectedOption)
    {
        if (IsPropertyEditorCommitSuppressed()
            || _viewModel is null
            || comboBox.DataContext is not FrontedPropertyEditorItem item)
        {
            return false;
        }

        var value = useSelectedOption && comboBox.SelectedItem is FrontedFontFamilyOption option
            ? option.Value
            : comboBox.Text;
        item.Value = value;
        item.EditText = comboBox.Text;
        return _viewModel.ApplyPropertyEdit(item, value);
    }

    private void PropertyColorPicker_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not DependencyObject picker)
        {
            return;
        }

        DependencyPropertyDescriptor
            .FromName("SelectedColor", picker.GetType(), picker.GetType())
            ?.AddValueChanged(picker, PropertyColorPicker_OnSelectedColorChanged);
    }

    private void PropertyColorPicker_OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not DependencyObject picker)
        {
            return;
        }

        DependencyPropertyDescriptor
            .FromName("SelectedColor", picker.GetType(), picker.GetType())
            ?.RemoveValueChanged(picker, PropertyColorPicker_OnSelectedColorChanged);
    }

    private void PropertyColorPicker_OnSelectedColorChanged(object? sender, EventArgs e)
    {
        if (sender is not FrameworkElement picker || !picker.IsKeyboardFocusWithin)
        {
            return;
        }

        if (IsPropertyEditorCommitSuppressed())
        {
            return;
        }

        if (picker.DataContext is FrontedPropertyEditorItem item)
        {
            item.EditText = FrontedPropertyColorHelper.ToArgbString(item.ColorValue);
        }
    }

    private bool ApplyPropertyEditorValue(object sender)
    {
        if (IsPropertyEditorCommitSuppressed()
            || _viewModel is null
            || sender is not FrameworkElement { DataContext: FrontedPropertyEditorItem item })
        {
            return false;
        }

        var value = sender is System.Windows.Controls.TextBox textBox
            ? textBox.Text
            : item.EditorKind is FrontedPropertyEditorKind.Text
                or FrontedPropertyEditorKind.Number
                ? item.EditText
                : item.Value;

        return _viewModel.ApplyPropertyEdit(item, value);
    }

    private bool IsPropertyEditorCommitSuppressed()
    {
        return !_isLoaded || _suppressPropertyEditorCommit || _viewModel?.IsRebuildingPropertyGrid == true;
    }

    private void SuppressPropertyEditorCommitForLayoutPass()
    {
        _suppressPropertyEditorCommit = true;
        Dispatcher.BeginInvoke(
            () => _suppressPropertyEditorCommit = false,
            DispatcherPriority.Loaded);
    }

    private void AttachViewModel()
    {
        if (_viewModel is not null)
        {
            return;
        }

        if (DataContext is not FrontedDesignerWindowViewModel viewModel)
        {
            return;
        }

        _viewModel = viewModel;
        _viewModel.PreviewRenderRequested += OnPreviewRenderRequested;
        _viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        _viewModel.PropertyEditorItems.CollectionChanged += PropertyEditorItems_OnCollectionChanged;
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FrontedDesignerWindowViewModel.IsRebuildingPropertyGrid)
            && _viewModel?.IsRebuildingPropertyGrid == true)
        {
            SuppressPropertyEditorCommitForLayoutPass();
        }

        if (e.PropertyName == nameof(FrontedDesignerWindowViewModel.SelectedDesignItem))
        {
            SuppressPropertyEditorCommitForLayoutPass();
            RebuildInteractionLayer();
            FocusDesignSurface();
        }

        if (e.PropertyName == nameof(FrontedDesignerWindowViewModel.BorderedImageResizeTarget))
        {
            RebuildInteractionLayer();
            FocusDesignSurface();
        }
    }

    private void PropertyEditorItems_OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SuppressPropertyEditorCommitForLayoutPass();
    }

    private void OpenValidationDetails_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (_validationDetailsWindow is null || !_validationDetailsWindow.IsVisible)
        {
            _validationDetailsWindow = new ValidationDetailsWindow
            {
                Owner = this,
                DataContext = _viewModel
            };
            _validationDetailsWindow.Closed += ValidationDetailsWindow_OnClosed;
            _validationDetailsWindow.Show();
            return;
        }

        _validationDetailsWindow.Activate();
    }

    private void ValidationDetailsWindow_OnClosed(object? sender, EventArgs e)
    {
        if (sender is ValidationDetailsWindow window && ReferenceEquals(window, _validationDetailsWindow))
        {
            window.Closed -= ValidationDetailsWindow_OnClosed;
            _validationDetailsWindow = null;
        }
    }

    private void CloseValidationDetailsWindowSafely()
    {
        var window = _validationDetailsWindow;
        if (window is null)
        {
            return;
        }

        window.Closed -= ValidationDetailsWindow_OnClosed;
        if (!window.IsVisible)
        {
            return;
        }

        try
        {
            window.Owner = null;
            window.Close();
        }
        catch (InvalidOperationException ex)
        {
            _logger?.LogWarning(ex, "Failed to close fronted designer validation details window safely.");
        }
    }

    private void AddControlButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (AddControlButton.ContextMenu is null)
        {
            return;
        }

        AddControlButton.ContextMenu.PlacementTarget = AddControlButton;
        AddControlButton.ContextMenu.IsOpen = true;
    }

    private void AddControlMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not System.Windows.Controls.MenuItem { Tag: string controlType })
        {
            return;
        }

        _viewModel.AddControlCommand.Execute(new FrontedAddControlRequest
        {
            ControlType = controlType,
            CenterX = GetViewportCenterX(),
            CenterY = GetViewportCenterY()
        });
        FocusDesignSurface();
    }

    private async void ReloadLayoutButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || !await ConfirmDirtyDocumentCanContinueAsync("SaveBeforeSwitch"))
        {
            return;
        }

        await _viewModel.ReloadLayoutCoreAsync();
        _lastAcceptedWindow = _viewModel.SelectedWindow;
        _lastAcceptedCanvas = _viewModel.SelectedCanvas;
    }

    private async void SaveLayoutButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var saved = await _viewModel.SaveCurrentLayoutAsync();
        if (!saved && _viewModel.ErrorCount > 0)
        {
            OpenValidationDetails_OnClick(sender, e);
        }
    }

    private async void ResetToBuiltInButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || !await ConfirmDirtyDocumentCanContinueAsync("SaveBeforeSwitch"))
        {
            return;
        }

        if (!await ConfirmResetToBuiltInAsync())
        {
            return;
        }

        await _viewModel.ResetToBuiltInCoreAsync();
        _lastAcceptedWindow = _viewModel.SelectedWindow;
        _lastAcceptedCanvas = _viewModel.SelectedCanvas;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_forceCloseAfterDirtyPrompt || _viewModel?.CurrentDocument?.IsDirty != true)
        {
            return;
        }

        e.Cancel = true;
        if (_isDirtyClosePromptOpen)
        {
            return;
        }

        _isDirtyClosePromptOpen = true;
        Dispatcher.BeginInvoke(
            new Action(async () => await PromptDirtyCloseAfterCancelAsync()),
            DispatcherPriority.Background);
    }

    private async Task PromptDirtyCloseAfterCancelAsync()
    {
        try
        {
            var result = await ShowDirtyPromptAsync("SaveBeforeClose");
            if (result == MessageBoxResult.Primary)
            {
                if (_viewModel is not null && await _viewModel.SaveCurrentLayoutAsync())
                {
                    _forceCloseAfterDirtyPrompt = true;
                    Close();
                }
            }
            else if (result == MessageBoxResult.Secondary)
            {
                _forceCloseAfterDirtyPrompt = true;
                Close();
            }
        }
        finally
        {
            _isDirtyClosePromptOpen = false;
        }
    }

    private async Task<bool> ConfirmDirtyDocumentCanContinueAsync(string messageKey)
    {
        if (_viewModel?.CurrentDocument?.IsDirty != true)
        {
            return true;
        }

        var result = await ShowDirtyPromptAsync(messageKey);
        if (result == MessageBoxResult.Primary)
        {
            return await _viewModel.SaveCurrentLayoutAsync();
        }

        return result == MessageBoxResult.Secondary;
    }

    private Task<MessageBoxResult> ShowDirtyPromptAsync(string messageKey)
    {
        return MessageBoxHelper.ShowThreeOptionAsync(
            I18nHelper.GetLocalizedString(messageKey),
            I18nHelper.GetLocalizedString("UnsavedChanges"),
            I18nHelper.GetLocalizedString("Save"),
            I18nHelper.GetLocalizedString("DiscardChanges"),
            I18nHelper.GetLocalizedString("Cancel"),
            width: 600,
            minWidth: 560,
            primaryButtonIcon: SymbolRegular.Save24,
            secondaryButtonIcon: SymbolRegular.Delete24,
            closeButtonIcon: SymbolRegular.Dismiss24);
    }

    private async Task<bool> ConfirmResetToBuiltInAsync()
    {
        var messageBox = new Wpf.Ui.Controls.MessageBox
        {
            Owner = this,
            Title = I18nHelper.GetLocalizedString("ResetToBuiltIn"),
            Content = I18nHelper.GetLocalizedString("ResetLayoutConfirm"),
            PrimaryButtonText = I18nHelper.GetLocalizedString("Confirm"),
            PrimaryButtonIcon = new SymbolIcon { Symbol = SymbolRegular.ArrowClockwise24 },
            CloseButtonText = I18nHelper.GetLocalizedString("Cancel"),
            CloseButtonIcon = new SymbolIcon { Symbol = SymbolRegular.Dismiss24 }
        };

        return await messageBox.ShowDialogAsync() == MessageBoxResult.Primary;
    }

    private async void RestartNowButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmDirtyDocumentCanContinueAsync("SaveBeforeRestart"))
        {
            return;
        }

        AppBase.Current.Restart();
    }

    private void UpdateShiftSnapState()
    {
        _viewModel?.UpdateShiftSnapActive(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
    }

    private void OnPreviewRenderRequested(
        object? sender,
        FrontedDesignerPreviewRenderRequestedEventArgs e)
    {
        if (_renderer is null || e.Config is null || e.Context is null)
        {
            ClearPreviewCanvas();
            return;
        }

        try
        {
            ConfigureDesignSurface(e.Config.CanvasWidth, e.Config.CanvasHeight);
            _renderer.RenderToCanvas(PreviewCanvas, e.Config, e.Context);
            PreviewCanvas.UpdateLayout();
            RebuildInteractionLayer();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to render fronted designer preview.");
            ClearPreviewCanvas();
            _viewModel?.ReportRenderFailure(ex);
        }
    }

    private void ClearPreviewCanvas()
    {
        PreviewCanvas.Children.Clear();
        PreviewCanvas.Background = null;
        ConfigureDesignSurface(640, 360);
        InteractionLayer.Children.Clear();
        _hitboxes.Clear();
        _resizeHandles.Clear();
        _selectionOutline = null;
        _selectionLabel = null;
        ResetPointerInteraction();
    }

    private void ConfigureDesignSurface(double width, double height)
    {
        DesignSurfaceGrid.Width = width;
        DesignSurfaceGrid.Height = height;
        PreviewCanvas.Width = width;
        PreviewCanvas.Height = height;
        InteractionLayer.Width = width;
        InteractionLayer.Height = height;
        UpdatePreviewWorkspaceSize();
        _viewModel?.UpdateFitZoom(PreviewScrollViewer.ViewportWidth, PreviewScrollViewer.ViewportHeight, width, height);
    }

    private void RebuildInteractionLayer()
    {
        InteractionLayer.Children.Clear();
        _hitboxes.Clear();
        _resizeHandles.Clear();
        _selectionOutline = null;
        _selectionLabel = null;

        if (_viewModel?.CurrentDocument is null)
        {
            return;
        }

        foreach (var entry in _viewModel.CurrentDocument.Controls.Select((item, index) => new { Item = item, Index = index }))
        {
            if (!entry.Item.IsSelectableInEditor)
            {
                continue;
            }

            var hitbox = CreateHitbox(entry.Item, entry.Index);
            _hitboxes[entry.Item] = hitbox;
            InteractionLayer.Children.Add(hitbox);
        }

        if (_viewModel.SelectedDesignItem is not null)
        {
            AddSelectionAdorner(_viewModel.SelectedDesignItem);
        }
    }

    private Border CreateHitbox(FrontedControlDesignItem item, int layoutOrder)
    {
        var bounds = ResolveItemBounds(item);
        var hitbox = new Border
        {
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(1),
            Width = bounds.Width,
            Height = bounds.Height,
            IsHitTestVisible = true,
            Tag = item
        };

        Canvas.SetLeft(hitbox, bounds.Left);
        Canvas.SetTop(hitbox, bounds.Top);
        Panel.SetZIndex(
            hitbox,
            FrontedDesignerEditorVisualHelper.GetHitboxZIndex(
                item.Config.ZIndex,
                layoutOrder,
                ReferenceEquals(item, _viewModel?.SelectedDesignItem)));
        hitbox.MouseLeftButtonDown += Hitbox_OnMouseLeftButtonDown;
        return hitbox;
    }

    private void AddSelectionAdorner(FrontedControlDesignItem item)
    {
        var bounds = ResolveItemBounds(item);

        _selectionOutline = new Border
        {
            Width = bounds.Width,
            Height = bounds.Height,
            BorderBrush = TryFindResource("AccentFillColorDefaultBrush") as Brush ?? Brushes.DeepSkyBlue,
            BorderThickness = new Thickness(FrontedDesignerEditorVisualHelper.SelectionBorderThickness),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(_selectionOutline, bounds.Left);
        Canvas.SetTop(_selectionOutline, bounds.Top);
        Panel.SetZIndex(_selectionOutline, FrontedDesignerEditorVisualHelper.SelectedOutlineZIndex);
        InteractionLayer.Children.Add(_selectionOutline);

        _selectionLabel = new Border
        {
            Background = TryFindResource("AccentFillColorDefaultBrush") as Brush ?? Brushes.DeepSkyBlue,
            Padding = new Thickness(4, 1, 4, 1),
            CornerRadius = new CornerRadius(2),
            Child = new System.Windows.Controls.TextBlock
            {
                Text = item.Name,
                FontSize = 11,
                Foreground = Brushes.White
            },
            IsHitTestVisible = false
        };
        Canvas.SetLeft(_selectionLabel, bounds.Left);
        Canvas.SetTop(_selectionLabel, Math.Max(0, bounds.Top - 18));
        Panel.SetZIndex(_selectionLabel, FrontedDesignerEditorVisualHelper.SelectedOutlineZIndex + 1);
        InteractionLayer.Children.Add(_selectionLabel);

        foreach (var handle in Enum.GetValues<FrontedDesignerResizeHandleKind>())
        {
            var handleElement = CreateResizeHandle(handle);
            _resizeHandles[handle] = handleElement;
            InteractionLayer.Children.Add(handleElement);
        }

        UpdateSelectedInteractionVisuals();
    }

    private Border CreateResizeHandle(FrontedDesignerResizeHandleKind handle)
    {
        var element = new Border
        {
            Width = FrontedDesignerEditorVisualHelper.HandleHitTargetSize,
            Height = FrontedDesignerEditorVisualHelper.HandleHitTargetSize,
            Background = Brushes.Transparent,
            Child = new Border
            {
                Width = FrontedDesignerEditorVisualHelper.HandleVisualSize,
                Height = FrontedDesignerEditorVisualHelper.HandleVisualSize,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Background = TryFindResource("AccentFillColorDefaultBrush") as Brush ?? Brushes.DeepSkyBlue,
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(FrontedDesignerEditorVisualHelper.HandleBorderThickness)
            },
            Cursor = GetCursor(handle),
            Tag = handle
        };

        Panel.SetZIndex(element, FrontedDesignerEditorVisualHelper.SelectedHandleZIndex);
        element.MouseLeftButtonDown += ResizeHandle_OnMouseLeftButtonDown;
        return element;
    }

    private void UpdateSelectedInteractionVisuals()
    {
        var item = _viewModel?.SelectedDesignItem;
        if (item is null)
        {
            return;
        }

        var bounds = ResolveItemBounds(item);

        if (_hitboxes.TryGetValue(item, out var hitbox))
        {
            hitbox.Width = bounds.Width;
            hitbox.Height = bounds.Height;
            Canvas.SetLeft(hitbox, bounds.Left);
            Canvas.SetTop(hitbox, bounds.Top);
            Panel.SetZIndex(hitbox, FrontedDesignerEditorVisualHelper.SelectedHitboxZIndex);
        }

        if (_selectionOutline is not null)
        {
            _selectionOutline.Width = bounds.Width;
            _selectionOutline.Height = bounds.Height;
            Canvas.SetLeft(_selectionOutline, bounds.Left);
            Canvas.SetTop(_selectionOutline, bounds.Top);
        }

        if (_selectionLabel is not null)
        {
            Canvas.SetLeft(_selectionLabel, bounds.Left);
            Canvas.SetTop(_selectionLabel, Math.Max(0, bounds.Top - 18));
        }

        SetHandlePosition(FrontedDesignerResizeHandleKind.TopLeft, bounds.Left, bounds.Top);
        SetHandlePosition(FrontedDesignerResizeHandleKind.Top, bounds.Left + bounds.Width / 2, bounds.Top);
        SetHandlePosition(FrontedDesignerResizeHandleKind.TopRight, bounds.Left + bounds.Width, bounds.Top);
        SetHandlePosition(FrontedDesignerResizeHandleKind.Left, bounds.Left, bounds.Top + bounds.Height / 2);
        SetHandlePosition(FrontedDesignerResizeHandleKind.Right, bounds.Left + bounds.Width, bounds.Top + bounds.Height / 2);
        SetHandlePosition(FrontedDesignerResizeHandleKind.BottomLeft, bounds.Left, bounds.Top + bounds.Height);
        SetHandlePosition(FrontedDesignerResizeHandleKind.Bottom, bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height);
        SetHandlePosition(FrontedDesignerResizeHandleKind.BottomRight, bounds.Left + bounds.Width, bounds.Top + bounds.Height);
    }

    private void SetHandlePosition(FrontedDesignerResizeHandleKind handle, double x, double y)
    {
        if (!_resizeHandles.TryGetValue(handle, out var element))
        {
            return;
        }

        Canvas.SetLeft(element, x - element.Width / 2);
        Canvas.SetTop(element, y - element.Height / 2);
    }

    private void Hitbox_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsSpacePressed())
        {
            BeginViewportPan(e);
            e.Handled = true;
            return;
        }

        if (sender is not FrameworkElement { Tag: FrontedControlDesignItem item } hitbox
            || _viewModel is null)
        {
            return;
        }

        FocusDesignSurface();
        BeginPendingHitboxClick(item, e.GetPosition(InteractionLayer), hitbox);
        e.Handled = true;
    }

    private void ResizeHandle_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: FrontedDesignerResizeHandleKind handle } element
            || _viewModel?.SelectedDesignItem is null)
        {
            return;
        }

        FocusDesignSurface();
        _activeResizeHandle = handle;
        BeginInteraction(InteractionMode.Resize, e.GetPosition(InteractionLayer), element);
        e.Handled = true;
    }

    private void InteractionLayer_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsSpacePressed())
        {
            BeginViewportPan(e);
            e.Handled = true;
            return;
        }

        if (ReferenceEquals(e.OriginalSource, InteractionLayer))
        {
            FocusDesignSurface();
            BeginPendingEmptyClick(e.GetPosition(InteractionLayer));
            e.Handled = true;
        }
    }

    private void InteractionLayer_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        BeginViewportPan(e);
        e.Handled = true;
    }

    private void DesignSurface_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        FocusDesignSurface();
    }

    private void InteractionLayer_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_isPanningViewport)
        {
            UpdateViewportPan(e);
            e.Handled = true;
            return;
        }

        if (_capturedElement is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var currentPosition = e.GetPosition(InteractionLayer);
        var deltaX = currentPosition.X - _startMousePosition.X;
        var deltaY = currentPosition.Y - _startMousePosition.Y;

        if (_pendingHitCandidate is not null)
        {
            HandlePendingHitboxMove(deltaX, deltaY);
        }
        else if (_isPendingEmptyClick)
        {
            _hasExceededClickThreshold |= FrontedDesignerInteractionHelper.ExceedsClickThreshold(deltaX, deltaY);
        }
        else if (_interactionMode == InteractionMode.Resize && _activeResizeHandle is { } handle)
        {
            _viewModel?.ResizeSelectedDesignItem(
                handle,
                _originalLeft,
                _originalTop,
                _originalWidth,
                _originalHeight,
                deltaX,
                deltaY,
                renderPreview: false);
            UpdateSelectedInteractionVisuals();
            UpdateSelectedPreviewElement();
        }

        e.Handled = true;
    }

    private void HandlePendingHitboxMove(double deltaX, double deltaY)
    {
        if (_viewModel?.SelectedDesignItem is null || _pendingHitCandidate is null)
        {
            return;
        }

        _hasExceededClickThreshold |= FrontedDesignerInteractionHelper.ExceedsClickThreshold(deltaX, deltaY);
        var action = FrontedDesignerInteractionHelper.ResolvePointerAction(
            _hasExceededClickThreshold,
            ReferenceEquals(_pendingHitCandidate, _viewModel.SelectedDesignItem),
            _hasStartedDrag);

        if (action == FrontedDesignerPointerAction.BeginDragSelected)
        {
            _viewModel.CaptureUndoSnapshot();
            _hasStartedDrag = true;
            _interactionMode = InteractionMode.Drag;
        }

        if (action is FrontedDesignerPointerAction.BeginDragSelected or FrontedDesignerPointerAction.DragSelected)
        {
            _viewModel.MoveSelectedDesignItem(
                _originalLeft,
                _originalTop,
                deltaX,
                deltaY,
                renderPreview: false);
            UpdateSelectedInteractionVisuals();
            UpdateSelectedPreviewElement();
        }
    }

    private void InteractionLayer_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanningViewport)
        {
            EndViewportPan();
            e.Handled = true;
            return;
        }

        if (_capturedElement is null)
        {
            return;
        }

        _capturedElement.ReleaseMouseCapture();

        if (_pendingHitCandidate is not null)
        {
            if (!_hasExceededClickThreshold)
            {
                _viewModel?.SelectDesignItem(_pendingHitCandidate);
            }
            else if (_hasStartedDrag)
            {
                _viewModel?.CommitDesignItemGeometryEdit();
            }
        }
        else if (_isPendingEmptyClick)
        {
            if (!_hasExceededClickThreshold)
            {
                _viewModel?.ClearSelection();
            }
        }
        else if (_interactionMode == InteractionMode.Resize)
        {
            _viewModel?.CommitDesignItemGeometryEdit();
        }

        ResetPointerInteraction();
        e.Handled = true;
    }

    private void InteractionLayer_OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanningViewport)
        {
            EndViewportPan();
            e.Handled = true;
        }
    }

    private void PreviewScrollViewer_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        BeginViewportPan(e);
        e.Handled = true;
    }

    private void PreviewScrollViewer_OnPreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanningViewport)
        {
            EndViewportPan();
            e.Handled = true;
        }
    }

    private void PreviewScrollViewer_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsSpacePressed())
        {
            return;
        }

        BeginViewportPan(e);
        e.Handled = true;
    }

    private void PreviewScrollViewer_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanningViewport)
        {
            EndViewportPan();
            e.Handled = true;
        }
    }

    private void PreviewScrollViewer_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanningViewport)
        {
            return;
        }

        UpdateViewportPan(e);
        e.Handled = true;
    }

    private void PreviewScrollViewer_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_viewModel is null || !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            return;
        }

        var oldScale = _viewModel.ZoomScale;
        var cursorPosition = e.GetPosition(PreviewScrollViewer);
        var oldHorizontalOffset = PreviewScrollViewer.HorizontalOffset;
        var oldVerticalOffset = PreviewScrollViewer.VerticalOffset;

        _viewModel.ZoomByWheelDelta(e.Delta);
        PreviewScrollViewer.UpdateLayout();

        if (oldScale > 0D && Math.Abs(_viewModel.ZoomScale - oldScale) > 0.0001D)
        {
            var ratio = _viewModel.ZoomScale / oldScale;
            PreviewScrollViewer.ScrollToHorizontalOffset((oldHorizontalOffset + cursorPosition.X) * ratio - cursorPosition.X);
            PreviewScrollViewer.ScrollToVerticalOffset((oldVerticalOffset + cursorPosition.Y) * ratio - cursorPosition.Y);
        }

        e.Handled = true;
    }

    private void PreviewScrollViewer_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdatePreviewWorkspaceSize();
        _viewModel?.UpdateFitZoom(PreviewScrollViewer.ViewportWidth, PreviewScrollViewer.ViewportHeight);
    }

    private void DesignSurface_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel?.SelectedDesignItem is null || ShouldIgnoreKeyboardInput())
        {
            return;
        }

        var step = GetKeyboardMoveStep();
        var handled = true;
        switch (e.Key)
        {
            case Key.Left:
                _viewModel.MoveSelectedDesignItemBy(-step, 0);
                break;
            case Key.Right:
                _viewModel.MoveSelectedDesignItemBy(step, 0);
                break;
            case Key.Up:
                _viewModel.MoveSelectedDesignItemBy(0, -step);
                break;
            case Key.Down:
                _viewModel.MoveSelectedDesignItemBy(0, step);
                break;
            case Key.Delete:
                _viewModel.DeleteSelectedControlCommand.Execute(null);
                break;
            default:
                handled = false;
                break;
        }

        if (handled)
        {
            FocusDesignSurface();
            e.Handled = true;
        }
    }

    private void BeginInteraction(InteractionMode mode, Point startMousePosition, FrameworkElement element)
    {
        var item = _viewModel?.SelectedDesignItem;
        if (item is null)
        {
            return;
        }

        _viewModel?.CaptureUndoSnapshot();
        _interactionMode = mode;
        _startMousePosition = startMousePosition;
        _originalLeft = item.Config.Left;
        _originalTop = item.Config.Top;
        var bounds = ResolveItemBounds(item);
        if (item.Config is BorderedImageFrontedControlConfig imageConfig
            && _viewModel?.BorderedImageResizeTarget == FrontedDesignerResizeTarget.Image)
        {
            imageConfig.ImageWidth ??= bounds.Width;
            imageConfig.ImageHeight ??= bounds.Height;
        }
        else if (!item.Config.Width.HasValue)
        {
            item.Config.Width = bounds.Width;

            if (!item.Config.Height.HasValue)
            {
                item.Config.Height = bounds.Height;
            }
        }
        else if (!item.Config.Height.HasValue)
        {
            item.Config.Height = bounds.Height;
        }

        _originalWidth = bounds.Width;
        _originalHeight = bounds.Height;
        _capturedElement = element;
        element.CaptureMouse();
    }

    private void BeginViewportPan(MouseEventArgs e)
    {
        if (_capturedElement is not null && _capturedElement.IsMouseCaptured)
        {
            _capturedElement.ReleaseMouseCapture();
        }

        ResetPointerInteraction();
        _isPanningViewport = true;
        _panStartViewportPosition = e.GetPosition(PreviewScrollViewer);
        _panStartHorizontalOffset = PreviewScrollViewer.HorizontalOffset;
        _panStartVerticalOffset = PreviewScrollViewer.VerticalOffset;
        _capturedElement = PreviewScrollViewer;
        _cursorBeforePan = PreviewScrollViewer.Cursor;
        PreviewScrollViewer.Cursor = Cursors.SizeAll;
        PreviewScrollViewer.CaptureMouse();
        FocusDesignSurface();
    }

    private void UpdateViewportPan(MouseEventArgs e)
    {
        var currentPosition = e.GetPosition(PreviewScrollViewer);
        var deltaX = currentPosition.X - _panStartViewportPosition.X;
        var deltaY = currentPosition.Y - _panStartViewportPosition.Y;
        PreviewScrollViewer.ScrollToHorizontalOffset(_panStartHorizontalOffset - deltaX);
        PreviewScrollViewer.ScrollToVerticalOffset(_panStartVerticalOffset - deltaY);
    }

    private void EndViewportPan()
    {
        if (_capturedElement is not null)
        {
            _capturedElement.ReleaseMouseCapture();
        }

        PreviewScrollViewer.Cursor = _cursorBeforePan;
        ResetPointerInteraction();
    }

    private void BeginPendingHitboxClick(
        FrontedControlDesignItem item,
        Point startMousePosition,
        FrameworkElement element)
    {
        ResetPointerInteraction();
        _pendingHitCandidate = item;
        _startMousePosition = startMousePosition;
        _originalLeft = item.Config.Left;
        _originalTop = item.Config.Top;
        var bounds = ResolveItemBounds(item);
        _originalWidth = bounds.Width;
        _originalHeight = bounds.Height;
        _capturedElement = element;
        element.CaptureMouse();
    }

    private void BeginPendingEmptyClick(Point startMousePosition)
    {
        ResetPointerInteraction();
        _isPendingEmptyClick = true;
        _startMousePosition = startMousePosition;
        _capturedElement = InteractionLayer;
        InteractionLayer.CaptureMouse();
    }

    private void ResetPointerInteraction()
    {
        _capturedElement = null;
        _interactionMode = InteractionMode.None;
        _activeResizeHandle = null;
        _pendingHitCandidate = null;
        _isPendingEmptyClick = false;
        _hasExceededClickThreshold = false;
        _hasStartedDrag = false;
        _isPanningViewport = false;
        _cursorBeforePan = null;
    }

    private void UpdatePreviewWorkspaceSize()
    {
        PreviewWorkspace.MinWidth = Math.Max(640D, PreviewScrollViewer.ViewportWidth);
        PreviewWorkspace.MinHeight = Math.Max(420D, PreviewScrollViewer.ViewportHeight);
    }

    private FrontedDesignerResolvedBounds ResolveItemBounds(FrontedControlDesignItem item)
    {
        var previewElement = FindPreviewElement(item.Name);
        if (item.Config is BorderedImageFrontedControlConfig imageConfig
            && _viewModel?.BorderedImageResizeTarget == FrontedDesignerResizeTarget.Image)
        {
            return ResolveBorderedImageInnerBounds(
                imageConfig,
                previewElement?.ActualWidth,
                previewElement?.ActualHeight);
        }

        return FrontedDesignerBoundsResolver.Resolve(
            item.Config,
            previewElement?.ActualWidth,
            previewElement?.ActualHeight);
    }

    private static FrontedDesignerResolvedBounds ResolveBorderedImageInnerBounds(
        BorderedImageFrontedControlConfig config,
        double? actualWidth,
        double? actualHeight)
    {
        var borderBounds = FrontedDesignerBoundsResolver.Resolve(config, actualWidth, actualHeight);
        var imageWidth = config.ImageWidth ?? borderBounds.Width;
        var imageHeight = config.ImageHeight ?? borderBounds.Height;
        var offsetX = ResolveAlignedOffset(borderBounds.Width, imageWidth, config.HorizontalAlignment, isHorizontal: true);
        var offsetY = ResolveAlignedOffset(borderBounds.Height, imageHeight, config.VerticalAlignment, isHorizontal: false);

        return new FrontedDesignerResolvedBounds(
            borderBounds.Left + offsetX,
            borderBounds.Top + offsetY,
            imageWidth,
            imageHeight);
    }

    private static double ResolveAlignedOffset(double slotSize, double elementSize, string? alignment, bool isHorizontal)
    {
        var normalized = alignment?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = "Center";
        }

        return normalized switch
        {
            "Right" when isHorizontal => slotSize - elementSize,
            "Bottom" when !isHorizontal => slotSize - elementSize,
            "Center" => (slotSize - elementSize) / 2D,
            _ => 0D
        };
    }

    private void UpdateSelectedPreviewElement()
    {
        var item = _viewModel?.SelectedDesignItem;
        if (item is null)
        {
            return;
        }

        var element = FindPreviewElement(item.Name);
        if (element is null)
        {
            return;
        }

        Canvas.SetLeft(element, item.Config.Left);
        Canvas.SetTop(element, item.Config.Top);

        if (item.Config.Width.HasValue)
        {
            element.Width = item.Config.Width.Value;
        }

        if (item.Config.Height.HasValue)
        {
            element.Height = item.Config.Height.Value;
        }

        if (item.Config is BorderedImageFrontedControlConfig imageConfig
            && _viewModel?.BorderedImageResizeTarget == FrontedDesignerResizeTarget.Image)
        {
            UpdateBorderedImageInnerPreviewElement(element, imageConfig);
        }

        var bounds = ResolveItemBounds(item);
        var linkedOverlays = _viewModel?.SyncLinkedOverlays(item, bounds) ?? [];
        foreach (var linkedOverlay in linkedOverlays)
        {
            UpdatePreviewElement(linkedOverlay);
        }
    }

    private static void UpdateBorderedImageInnerPreviewElement(
        FrameworkElement rootElement,
        BorderedImageFrontedControlConfig config)
    {
        var innerImage = FindDescendant<System.Windows.Controls.Image>(rootElement);
        if (innerImage is null)
        {
            return;
        }

        if (config.ImageWidth.HasValue)
        {
            innerImage.Width = config.ImageWidth.Value;
        }

        if (config.ImageHeight.HasValue)
        {
            innerImage.Height = config.ImageHeight.Value;
        }
    }

    private void UpdatePreviewElement(FrontedControlDesignItem item)
    {
        var element = FindPreviewElement(item.Name);
        if (element is null)
        {
            return;
        }

        Canvas.SetLeft(element, item.Config.Left);
        Canvas.SetTop(element, item.Config.Top);

        if (item.Config.Width.HasValue)
        {
            element.Width = item.Config.Width.Value;
        }

        if (item.Config.Height.HasValue)
        {
            element.Height = item.Config.Height.Value;
        }
    }

    private FrameworkElement? FindPreviewElement(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        if (PreviewCanvas.FindName(name) is FrameworkElement canvasNameMatch)
        {
            return canvasNameMatch;
        }

        if (Window.GetWindow(PreviewCanvas) is FrameworkElement window
            && window.FindName(name) is FrameworkElement windowNameMatch)
        {
            return windowNameMatch;
        }

        return FindGeneratedPreviewElement(PreviewCanvas, name);
    }

    private static FrameworkElement? FindGeneratedPreviewElement(DependencyObject parent, string name)
    {
        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is FrameworkElement element
                && (element.Name == name || FrontedRendererProperties.GetRegisteredName(element) == name))
            {
                return element;
            }

            var nested = FindGeneratedPreviewElement(child, name);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static T? FindDescendant<T>(DependencyObject parent)
        where T : DependencyObject
    {
        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
            {
                return match;
            }

            var nested = FindDescendant<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private void FocusDesignSurface()
    {
        InteractionLayer.Focus();
    }

    private double? GetViewportCenterX()
    {
        if (_viewModel?.CurrentDocument is null || _viewModel.ZoomScale <= 0D)
        {
            return null;
        }

        var value = (PreviewScrollViewer.HorizontalOffset + PreviewScrollViewer.ViewportWidth / 2D) / _viewModel.ZoomScale;
        return double.IsFinite(value) ? value : null;
    }

    private double? GetViewportCenterY()
    {
        if (_viewModel?.CurrentDocument is null || _viewModel.ZoomScale <= 0D)
        {
            return null;
        }

        var value = (PreviewScrollViewer.VerticalOffset + PreviewScrollViewer.ViewportHeight / 2D) / _viewModel.ZoomScale;
        return double.IsFinite(value) ? value : null;
    }

    private static Cursor GetCursor(FrontedDesignerResizeHandleKind handle)
    {
        return handle switch
        {
            FrontedDesignerResizeHandleKind.TopLeft
                or FrontedDesignerResizeHandleKind.BottomRight => Cursors.SizeNWSE,
            FrontedDesignerResizeHandleKind.TopRight
                or FrontedDesignerResizeHandleKind.BottomLeft => Cursors.SizeNESW,
            FrontedDesignerResizeHandleKind.Left
                or FrontedDesignerResizeHandleKind.Right => Cursors.SizeWE,
            FrontedDesignerResizeHandleKind.Top
                or FrontedDesignerResizeHandleKind.Bottom => Cursors.SizeNS,
            _ => Cursors.Arrow
        };
    }

    private double GetKeyboardMoveStep()
    {
        if (_viewModel?.EffectiveSnapEnabled == true)
        {
            return _viewModel.SnapGridSize;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            return 10D;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            return 1D;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            return 0.1D;
        }

        return FrontedDesignerGeometryHelper.CoordinateStep;
    }

    private static bool IsSpacePressed()
    {
        return Keyboard.IsKeyDown(Key.Space);
    }

    private static bool ShouldIgnoreKeyboardInput()
    {
        if (Keyboard.FocusedElement is not DependencyObject focused)
        {
            return false;
        }

        return FindAncestorOrSelf<System.Windows.Controls.TextBox>(focused) is not null
               || FindAncestorOrSelf<System.Windows.Controls.ComboBox>(focused) is not null
               || FindAncestorOrSelf<System.Windows.Controls.DataGrid>(focused) is not null
               || HasAncestorInNamespace(focused, "ColorPicker");
    }

    private static T? FindAncestorOrSelf<T>(DependencyObject current)
        where T : DependencyObject
    {
        var node = current;
        while (node is not null)
        {
            if (node is T match)
            {
                return match;
            }

            node = VisualTreeHelper.GetParent(node);
        }

        return null;
    }

    private static bool HasAncestorInNamespace(DependencyObject current, string namespacePrefix)
    {
        var node = current;
        while (node is not null)
        {
            if (node.GetType().Namespace?.StartsWith(namespacePrefix, StringComparison.Ordinal) == true)
            {
                return true;
            }

            node = VisualTreeHelper.GetParent(node);
        }

        return false;
    }

    private enum InteractionMode
    {
        None,
        Drag,
        Resize
    }
}
