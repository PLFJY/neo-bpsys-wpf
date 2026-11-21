using System.Windows.Data;
using neo_bpsys_wpf.Core.Models;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>为 BP 前台角色绑定提供按动画进度更新的展示值。</summary>
public interface IFrontedCharacterPresentationService
{
    /// <summary>为支持的角色绑定路径建立展示绑定。</summary>
    /// <param name="path">原布局绑定路径。</param>
    /// <param name="source">权威共享数据源。</param>
    /// <param name="binding">成功时返回展示绑定。</param>
    /// <returns>路径是受管理的角色绑定时为 true。</returns>
    bool TryCreateBinding(string path, ISharedDataService source, out Binding? binding);

    /// <summary>获取按动画进度更新的求生者展示槽位。</summary>
    /// <returns>四个求生者展示对象。</returns>
    IReadOnlyList<Player> GetSurvivorPlayers();

    /// <summary>获取按动画进度更新的监管者展示槽位。</summary>
    /// <returns>监管者展示对象。</returns>
    Player GetHunterPlayer();
}
