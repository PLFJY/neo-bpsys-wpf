using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 扫描和更新 v3 前台布局控件间的显式名称引用。
/// </summary>
public class FrontedLayoutReferenceScanner
{
    private IReadOnlyList<FrontedControlDesignItem> _controls = [];

    /// <summary>
    /// 初始化空引用扫描器。
    /// </summary>
    public FrontedLayoutReferenceScanner()
    {
    }

    /// <summary>
    /// 初始化带设计项集合的引用扫描器。
    /// </summary>
    public FrontedLayoutReferenceScanner(IEnumerable<FrontedControlDesignItem> controls)
    {
        SetControls(controls);
    }

    /// <summary>
    /// 设置后续增量查询和重命名操作使用的设计项集合。
    /// </summary>
    public void SetControls(IEnumerable<FrontedControlDesignItem> controls)
    {
        _controls = controls.ToArray();
    }

    /// <summary>
    /// 获取指定设计项集合中的全部已知引用。
    /// </summary>
    public IReadOnlyList<FrontedControlReference> GetReferences(IEnumerable<FrontedControlDesignItem> controls)
    {
        return controls
            .SelectMany(GetReferences)
            .ToArray();
    }

    /// <summary>
    /// 获取当前集合中指向指定控件名的引用。
    /// </summary>
    public IReadOnlyList<FrontedControlReference> GetIncomingReferences(string controlName)
    {
        return GetReferences(_controls)
            .Where(reference => reference.TargetControlName == controlName)
            .ToArray();
    }

    /// <summary>
    /// 判断当前集合是否可以把控件名从 oldName 改为 newName。
    /// </summary>
    public bool CanRenameControl(string oldName, string newName)
    {
        if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName))
        {
            return false;
        }

        return _controls.Any(control => control.Name == oldName)
               && !_controls.Any(control => control.Name == newName && control.Name != oldName);
    }

    /// <summary>
    /// 更新当前集合中所有已知引用字段。
    /// </summary>
    public void ApplyRenameReferences(string oldName, string newName)
    {
        foreach (var control in _controls)
        {
            if (control.Config is PickingBorderOverlayControlConfig pickingBorder
                && pickingBorder.TargetControlName == oldName)
            {
                pickingBorder.TargetControlName = newName;
            }
        }
    }

    private static IEnumerable<FrontedControlReference> GetReferences(FrontedControlDesignItem control)
    {
        if (control.Config is PickingBorderOverlayControlConfig pickingBorder
            && !string.IsNullOrWhiteSpace(pickingBorder.TargetControlName))
        {
            yield return new FrontedControlReference
            {
                SourceControlName = control.Name,
                PropertyName = nameof(PickingBorderOverlayControlConfig.TargetControlName),
                TargetControlName = pickingBorder.TargetControlName
            };
        }
    }
}
