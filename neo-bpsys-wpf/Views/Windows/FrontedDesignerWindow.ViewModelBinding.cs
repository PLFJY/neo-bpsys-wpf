using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Events;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.ViewModels.Windows;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
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
/// FrontedDesignerWindow 的ViewModelBinding业务交互逻辑。
/// </summary>
public partial class FrontedDesignerWindow
{
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
        _viewModel.DesignerGeometryPatchRequested += OnDesignerGeometryPatchRequested;
        _viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        _viewModel.PropertyEditorItems.CollectionChanged += PropertyEditorItems_OnCollectionChanged;
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FrontedDesignerWindowViewModel.IsRebuildingPropertyGrid)
            && _viewModel is { } propertyGridViewModel)
        {
            if (propertyGridViewModel.IsRebuildingPropertyGrid)
            {
                SuppressPropertyEditorCommitForLayoutPass();
            }
            else
            {
                _propertyGridReady?.TrySetResult();
            }
        }

        if (e.PropertyName == nameof(FrontedDesignerWindowViewModel.SelectedDesignItem))
        {
            SuppressPropertyEditorCommitForLayoutPass();
            var currentItem = _viewModel?.SelectedDesignItem;
            var selectionChanged = !ReferenceEquals(_lastSeenSelectedDesignItem, currentItem);
            var isUserSelection = _userSelectionDepth > 0;
            _lastSeenSelectedDesignItem = currentItem;

            if (_viewModel?.IsRestoringSnapshotVisuals == true)
            {
                return;
            }

            RebuildInteractionLayer();
            _viewModel?.UpdateBehaviorPreviewAnimationScope(PreviewCanvas);
            FocusDesignSurface();

            if (_initialLayoutLoaded
                && selectionChanged
                && currentItem is not null
                && isUserSelection)
            {
                TryQueuePropertyPanelTutorial();
            }
        }

        if (e.PropertyName == nameof(FrontedDesignerWindowViewModel.SelectedDesignItems))
        {
            if (_viewModel?.IsRestoringSnapshotVisuals == true)
            {
                return;
            }

            RebuildInteractionLayer();
            FocusDesignSurface();
        }

        if (e.PropertyName == nameof(FrontedDesignerWindowViewModel.SelectedTarget)
            || e.PropertyName == nameof(FrontedDesignerWindowViewModel.IsSubControlSelected))
        {
            if (_viewModel?.IsRestoringSnapshotVisuals == true)
            {
                return;
            }

            RebuildInteractionLayer();
        }

        if (e.PropertyName == nameof(FrontedDesignerWindowViewModel.ZoomScale))
        {
            UpdateSelectedInteractionVisuals();
            UpdateSubControlSelectionVisuals();
            RenderSnapGuides();
            ResetPreviewScrollOffsetForFitMode();
        }

        if (e.PropertyName == nameof(FrontedDesignerWindowViewModel.ActiveSnapGuides))
        {
            RenderSnapGuides();
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

    private void BehaviorExpander_OnExpanded(object sender, RoutedEventArgs e)
    {
        if (_behaviorPanelTutorialTask is { IsCompleted: false }
            || _viewModel?.SelectedDesignItem == null
            || !BehaviorExpander.IsExpanded)
        {
            return;
        }

        _behaviorPanelTutorialTask = RunBehaviorPanelTutorialAsync();
    }

}
