using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 存储窗口级的设计器 v3 布局选项。
/// </summary>
/// <remarks>
/// <c>windowTypeName</c> 必须是可安全映射到布局根目录内的 Canonical 标识；实现应复用布局路径助手，
/// 拒绝绝对路径、目录分隔符、<c>..</c> 和 Windows 保留名。当前默认实现已按此约定校验。
/// </remarks>
public interface IFrontedWindowLayoutOptionsService
{
    /// <summary>
    /// 加载指定窗口的布局选项。
    /// </summary>
    /// <param name="windowTypeName">Canonical 窗口标识。</param>
    /// <returns>窗口布局选项；不存在或读取失败时返回默认选项。</returns>
    FrontedWindowLayoutOptions LoadOptions(string windowTypeName);

    /// <summary>
    /// 保存指定窗口的布局选项。
    /// </summary>
    /// <param name="windowTypeName">窗口类型名。</param>
    /// <param name="options">要保存的布局选项。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task SaveOptionsAsync(
        string windowTypeName,
        FrontedWindowLayoutOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定窗口的用户选项文件路径。
    /// </summary>
    /// <param name="windowTypeName">窗口类型名。</param>
    /// <returns>用户选项文件路径。</returns>
    string GetUserOptionsPath(string windowTypeName);

    /// <summary>
    /// 重置指定窗口的布局选项为默认值。
    /// </summary>
    /// <param name="windowTypeName">窗口类型名。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task ResetOptionsAsync(string windowTypeName, CancellationToken cancellationToken = default);
}
