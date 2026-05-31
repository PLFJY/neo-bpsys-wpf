namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// v3 前台布局运行时关键名称目录。
/// </summary>
public class FrontedLayoutRuntimeContractCatalog
{
    private static readonly IReadOnlyDictionary<(string WindowTypeName, string CanvasName), IReadOnlySet<string>>
        RuntimeCriticalNames =
            new Dictionary<(string WindowTypeName, string CanvasName), IReadOnlySet<string>>
            {
                [("BpWindow", "BaseCanvas")] = new HashSet<string>(StringComparer.Ordinal)
                {
                    // These names are required by AnimationService until animation lookup becomes metadata-based.
                    "SurPick0",
                    "SurPick1",
                    "SurPick2",
                    "SurPick3",
                    "HunPick",
                    "SurPickingBorder0",
                    "SurPickingBorder1",
                    "SurPickingBorder2",
                    "SurPickingBorder3",
                    "HunPickingBorder"
                }
            };

    /// <summary>
    /// 获取指定窗口 Canvas 的运行时关键控件名。
    /// </summary>
    public IReadOnlySet<string> GetRuntimeCriticalNames(string windowTypeName, string canvasName)
    {
        return RuntimeCriticalNames.TryGetValue((windowTypeName, canvasName), out var names)
            ? names
            : new HashSet<string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// 判断指定控件名是否属于运行时关键名称。
    /// </summary>
    public bool IsRuntimeCritical(string windowTypeName, string canvasName, string controlName)
    {
        return GetRuntimeCriticalNames(windowTypeName, canvasName).Contains(controlName);
    }
}
