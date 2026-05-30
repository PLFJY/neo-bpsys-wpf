using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 默认 v3 前台控件工厂注册表。
/// </summary>
public class FrontedControlRegistry : IFrontedControlRegistry
{
    private readonly Dictionary<string, IFrontedControl> _controls;

    /// <summary>
    /// 初始化控件工厂注册表。
    /// </summary>
    public FrontedControlRegistry(IEnumerable<IFrontedControl> controls)
    {
        _controls = new Dictionary<string, IFrontedControl>(StringComparer.OrdinalIgnoreCase);
        foreach (var control in controls)
        {
            _controls[control.ControlType] = control;
        }
    }

    /// <inheritdoc />
    public IFrontedControl? GetControl(string controlType)
    {
        return _controls.GetValueOrDefault(controlType);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<IFrontedControl> GetControls()
    {
        return _controls.Values.ToArray();
    }
}
