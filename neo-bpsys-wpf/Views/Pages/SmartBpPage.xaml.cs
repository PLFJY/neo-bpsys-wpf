using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.ViewModels.Pages;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows;

namespace neo_bpsys_wpf.Views.Pages;

/// <summary>
/// SmartBpPage.xaml 的交互逻辑
/// </summary>
[BackendPageInfo("6E5AB941-A4A0-4D43-B9CB-381364414C1B",
    "SmartBp",
    Wpf.Ui.Controls.SymbolRegular.ScanText24,
    Core.Enums.BackendPageCategory.External)]
public partial class SmartBpPage : Page
{
    private readonly DependencyPropertyDescriptor? _moduleContentDescriptor;
    private SmartBpPageViewModel? _attachedViewModel;
    private bool _isModuleContentHandlerAttached;

    public SmartBpPage()
    {
        InitializeComponent();
        _moduleContentDescriptor = DependencyPropertyDescriptor.FromProperty(
            ContentControl.ContentProperty,
            typeof(ContentControl));
        Loaded += (_, _) =>
        {
            AttachViewModel(DataContext);
            AttachModuleContentHandler();
            TutorialSignalPublisher.Publish(TutorialSignalIds.NavigationSmartBpOpened);
            TutorialPageLoader.RunPendingOnLoaded(this, TutorialPageKeys.SmartBp, "Loaded");
        };
        IsVisibleChanged += (_, e) =>
        {
            if (Equals(e.NewValue, true))
            {
                TutorialPageLoader.RunPendingOnLoaded(this, TutorialPageKeys.SmartBp, "Visible");
            }
        };
        DataContextChanged += OnDataContextChanged;
        Unloaded += (_, _) =>
        {
            DetachViewModel(DataContext);
            DetachModuleContentHandler();
        };
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        DetachViewModel(e.OldValue);
        AttachViewModel(e.NewValue);
    }

    private void AttachViewModel(object? value)
    {
        if (ReferenceEquals(_attachedViewModel, value))
        {
            return;
        }

        DetachViewModel(_attachedViewModel);
        if (value is SmartBpPageViewModel viewModel)
        {
            viewModel.PropertyChanged += ViewModelOnPropertyChanged;
            _attachedViewModel = viewModel;
        }
    }

    private void DetachViewModel(object? value)
    {
        if (value is SmartBpPageViewModel viewModel && ReferenceEquals(_attachedViewModel, viewModel))
        {
            viewModel.PropertyChanged -= ViewModelOnPropertyChanged;
            _attachedViewModel = null;
        }
    }

    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SmartBpPageViewModel.IsModuleLoaded)
            && sender is SmartBpPageViewModel { IsModuleLoaded: true })
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(() => TutorialPageLoader.RunPendingOnLoaded(
                    this,
                    TutorialPageKeys.SmartBp,
                    "ModuleLoaded")));
        }
    }

    private void OnModuleContentChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(() => TutorialPageLoader.RunPendingOnLoaded(
                this,
                TutorialPageKeys.SmartBp,
                "ContentChanged")));
    }

    private void AttachModuleContentHandler()
    {
        if (_isModuleContentHandlerAttached)
        {
            return;
        }

        _moduleContentDescriptor?.AddValueChanged(SmartBpModuleContentHost, OnModuleContentChanged);
        _isModuleContentHandlerAttached = true;
    }

    private void DetachModuleContentHandler()
    {
        if (!_isModuleContentHandlerAttached)
        {
            return;
        }

        _moduleContentDescriptor?.RemoveValueChanged(SmartBpModuleContentHost, OnModuleContentChanged);
        _isModuleContentHandlerAttached = false;
    }
}
