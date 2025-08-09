using System.Collections.ObjectModel;
using System.Windows.Controls;
using neo_bpsys_wpf.Core.Abstractions.ViewModels;
using neo_bpsys_wpf.Core.Extensions;

namespace neo_bpsys_wpf.ViewModels.Pages;

public class ExtensionPageViewModel : ViewModelBase
{
    public string LoadedExtensionsCount => 
        $"{ExtensionManager.Instance().ReadOnlyExtensions.Count} 个扩展已加载";
    
    // 计算已启用的扩展数量
    
    public string EnabledExtensionsCount => 
        $"{ExtensionManager.Instance().ReadOnlyExtensions.Count(kv => kv.Value)} 个扩展已启用";
    public ObservableCollection<Border> ExtensionUIs => 
        ExtensionManager.Instance().ExtensionUIs;
}