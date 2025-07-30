using System.Collections.ObjectModel;
using System.ComponentModel;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models;

namespace neo_bpsys_wpf.Core.Extensions;

/// <summary>
/// 一个用于管理扩展的单例类，通过单例的方式确保全局只有一个实例。
/// 该类提供了注册和注销扩展的方法，并且可以设置共享数据服务
/// </summary>
public class ExtensionManager
{
    private static ExtensionManager _instance;
    private static readonly object Lock = new();
    
    public IReadOnlySharedDataService SharedDataService;

    private ObservableCollection<IExtension> Extensions { get; } = new();
    
    private ExtensionManager(ISharedDataService sharedDataService)
    {
        SharedDataService = new ReadOnlySharedDataService(sharedDataService);
    }
    
    public static ExtensionManager Instance(ISharedDataService sharedDataService)
    {
        lock (Lock) { 
            return _instance ??= new ExtensionManager(sharedDataService);
        }
    }
    
    public void SetSharedDataService(ISharedDataService sharedDataService)
    {
        lock (Lock)
        {
            SharedDataService = new ReadOnlySharedDataService(sharedDataService);
        }
    }
    
    public void RegisterExtension(IExtension extension)
    {
        ArgumentNullException.ThrowIfNull(extension);
        
        lock (Lock)
        {
            if (!Extensions.Contains(extension))
            {
                Extensions.Add(extension);
            }
        }
    }
    public void UnregisterExtension(IExtension extension)
    {
        ArgumentNullException.ThrowIfNull(extension);
        
        lock (Lock)
        {
            if (Extensions.Contains(extension))
            {
                Extensions.Remove(extension);
            }
        }
    }
}

/// <summary>
/// 只读的共享数据服务实现，提供对共享数据的只读访问。
/// 该类实现了 <see cref="IReadOnlySharedDataService"/> 接口，并将所有属性委托给传入的 <see cref="ISharedDataService"/> 实例。
/// 这样可以确保外部代码只能读取数据，而不能修改数据，从而保护共享数据的完整性。
/// </summary>
/// <param name="sharedDataService"></param>
public class ReadOnlySharedDataService(ISharedDataService sharedDataService) : IReadOnlySharedDataService
{
    // 实现 IReadOnlySharedDataService 的所有属性，返回 _sharedDataService 的对应值
    public Team MainTeam => sharedDataService.MainTeam;
    public Team AwayTeam => sharedDataService.AwayTeam;
    public Game CurrentGame => sharedDataService.CurrentGame;
    public IReadOnlyDictionary<string, Character> CharacterList => new ReadOnlyDictionary<string, Character>(sharedDataService.CharacterList);
    public IReadOnlyDictionary<string, Character> SurCharaList => new ReadOnlyDictionary<string, Character>(sharedDataService.SurCharaList);
    public IReadOnlyDictionary<string, Character> HunCharaList => new ReadOnlyDictionary<string, Character>(sharedDataService.HunCharaList);
    public ReadOnlyObservableCollection<bool> CanCurrentSurBanned => new ReadOnlyObservableCollection<bool>(sharedDataService.CanCurrentSurBanned);
    public ReadOnlyObservableCollection<bool> CanCurrentHunBanned => new ReadOnlyObservableCollection<bool>(sharedDataService.CanCurrentHunBanned);
    public ReadOnlyObservableCollection<bool> CanGlobalSurBanned => new ReadOnlyObservableCollection<bool>(sharedDataService.CanGlobalSurBanned);
    public ReadOnlyObservableCollection<bool> CanGlobalHunBanned => new ReadOnlyObservableCollection<bool>(sharedDataService.CanGlobalHunBanned);
    public bool IsTraitVisible => sharedDataService.IsTraitVisible;
    public string RemainingSeconds => sharedDataService.RemainingSeconds;
    public bool IsBo3Mode => sharedDataService.IsBo3Mode;
    public double GlobalScoreTotalMargin => sharedDataService.GlobalScoreTotalMargin;
}