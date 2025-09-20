using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Abstractions.ViewModels;
using neo_bpsys_wpf.Services;

namespace neo_bpsys_wpf.ViewModels.Pages;

public class ExtensionPageViewModel : ViewModelBase
{
    public IExtensionService ExtensionService { get; init; }
    public ExtensionPageViewModel(IExtensionService service)
    {
        ExtensionService = service;
        ExtensionService.ExtensionsChanged += (sender, args) =>
        {
            OnPropertyChanged(nameof(LoadedExtensionsCount));
            OnPropertyChanged(nameof(EnabledExtensionsCount));
            OnPropertyChanged(nameof(Extensions));
        };
        ExtensionService.ExtensionUIsUpdatedEvent += (sender, args) =>
        {
            OnPropertyChanged(nameof(ExtensionUIs));
        };
        
        EnableExtensionCommand = new RelayCommand<KeyValuePair<IExtension, bool>>(EnableExtension);
        DisableExtensionCommand = new RelayCommand<KeyValuePair<IExtension, bool>>(DisableExtension);
    }

    public int LoadedExtensionsCount => ExtensionService.ReadOnlyExtensions.Count;
    
    // 计算已启用的扩展数量

    public int EnabledExtensionsCount => ExtensionService.ReadOnlyExtensions.Count(ext => ext.Value);
    
    public ReadOnlyDictionary<IExtension, bool> Extensions => ExtensionService.ReadOnlyExtensions;
    public ObservableCollection<Border> ExtensionUIs => 
        ExtensionService.ExtensionUIs;
    
    
    public ICommand EnableExtensionCommand { get; private set; }

    private void EnableExtension(KeyValuePair<IExtension, bool> extension)
    {
        ExtensionService.EnableExtension(extension.Key);
    }
    
    public ICommand DisableExtensionCommand { get; private set; }

    private void DisableExtension(KeyValuePair<IExtension, bool> extension)
    {
        ExtensionService.DisableExtension(extension.Key);
    }
}