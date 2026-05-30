namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// v3 前台控件工厂注册表。
/// </summary>
public interface IFrontedControlRegistry
{
    /// <summary>
    /// 按控件类型获取控件工厂。
    /// </summary>
    IFrontedControl? GetControl(string controlType);

    /// <summary>
    /// 获取所有控件工厂。
    /// </summary>
    IReadOnlyCollection<IFrontedControl> GetControls();
}
