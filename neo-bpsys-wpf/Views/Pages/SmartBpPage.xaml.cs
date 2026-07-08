using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.ViewModels.Pages;
using System.ComponentModel;
using System.Windows.Controls;

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
    public SmartBpPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            TutorialSignalPublisher.Publish(TutorialSignalIds.NavigationSmartBpOpened);
            TutorialPageLoader.RunPendingOnLoaded(this, TutorialPageKeys.SmartBp);
        };
        IsVisibleChanged += (_, e) =>
        {
            if (Equals(e.NewValue, true))
            {
                TutorialPageLoader.RunPendingOnLoaded(this, TutorialPageKeys.SmartBp);
            }
        };
        DataContextChanged += OnDataContextChanged;
        Unloaded += (_, _) => DetachViewModel(DataContext);
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        DetachViewModel(e.OldValue);
        AttachViewModel(e.NewValue);
    }

    private void AttachViewModel(object? value)
    {
        if (value is SmartBpPageViewModel viewModel)
        {
            viewModel.PropertyChanged += ViewModelOnPropertyChanged;
        }
    }

    private void DetachViewModel(object? value)
    {
        if (value is SmartBpPageViewModel viewModel)
        {
            viewModel.PropertyChanged -= ViewModelOnPropertyChanged;
        }
    }

    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SmartBpPageViewModel.IsModuleLoaded)
            && sender is SmartBpPageViewModel { IsModuleLoaded: true })
        {
            TutorialPageLoader.RunPendingOnLoaded(this, TutorialPageKeys.SmartBp);
        }
    }
}
