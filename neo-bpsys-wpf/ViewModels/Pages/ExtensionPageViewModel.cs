using System.Collections.ObjectModel;
using System.Windows.Controls;
using neo_bpsys_wpf.Core.Abstractions.ViewModels;
using neo_bpsys_wpf.Core.Extensions;

namespace neo_bpsys_wpf.ViewModels.Pages;

public class ExtensionPageViewModel : ViewModelBase
{
    public ObservableCollection<Border> ExtensionUIs => 
        ExtensionManager.Instance().ExtensionUIs;
}