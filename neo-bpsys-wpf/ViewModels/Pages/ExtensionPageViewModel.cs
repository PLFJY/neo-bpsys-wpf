using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Abstractions.ViewModels;
using neo_bpsys_wpf.Core.Extensions;
using neo_bpsys_wpf.Core.Services;

namespace neo_bpsys_wpf.ViewModels.Pages;

public class ExtensionPageViewModel : ViewModelBase
{

    public ExtensionPageViewModel()
    {
        ExtensionManager.Instance().ExtensionsChanged += (sender, args) =>
        {
            OnPropertyChanged(nameof(LoadedExtensionsCount));
            OnPropertyChanged(nameof(EnabledExtensionsCount));
            OnPropertyChanged(nameof(Extensions));
        };
        ExtensionManager.Instance().ExtensionUIsUpdatedEvent += (sender, args) =>
        {
            OnPropertyChanged(nameof(ExtensionUIs));
        };
        
        EnableExtensionCommand = new RelayCommand<KeyValuePair<IExtension, bool>>(EnableExtension);
        DisableExtensionCommand = new RelayCommand<KeyValuePair<IExtension, bool>>(DisableExtension);
    }

    public int LoadedExtensionsCount => ExtensionManager.Instance().ReadOnlyExtensions.Count;
    
    // 计算已启用的扩展数量

    public int EnabledExtensionsCount => ExtensionManager.Instance().ReadOnlyExtensions.Count(ext => ext.Value);
    
    public ReadOnlyDictionary<IExtension, bool> Extensions => ExtensionManager.Instance().ReadOnlyExtensions;
    public ObservableCollection<Border> ExtensionUIs => 
        ExtensionManager.Instance().ExtensionUIs;
    
    
    public ICommand EnableExtensionCommand { get; private set; }

    private void EnableExtension(KeyValuePair<IExtension, bool> extension)
    {
        ExtensionManager.Instance().EnableExtension(extension.Key);
    }
    
    public ICommand DisableExtensionCommand { get; private set; }

    private void DisableExtension(KeyValuePair<IExtension, bool> extension)
    {
        ExtensionManager.Instance().DisableExtension(extension.Key);
    }
}