namespace neo_bpsys_wpf.Core.Attributes;

/// <summary>
/// Legacy canvas metadata for older fronted window attributes.
/// </summary>
public class CanvasName
{
    /// <summary>
    /// 画布名称信息
    /// </summary>
    /// <param name="name">画布名称</param>
    /// <param name="displayName">显示名称（可选）</param>
    public CanvasName(string name, string? displayName = null)
    {
        Name = name;
        if (name == "BaseCanvas")
            DisplayName = string.Empty;
        else
        {
            if (displayName != null) DisplayName = " " + displayName;
            DisplayName ??= " " + name;
        }
    }

    /// <summary>
    /// 画布名称信息
    /// </summary>
    public string Name { get; }
    
    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName { get; }
}

/// <summary>
/// 前台窗口信息
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class FrontedWindowInfo : Attribute
{

    private void Initialize(string id, string name, string[]? canvas, bool isBuiltin)
    {
        Name = name;
        Id = id;
        IsBuiltIn = isBuiltin;
        if (canvas != null)
        {
            var canvasList = new List<CanvasName>();
            foreach (var item in canvas)
            {
                var parts = item.Split('|');
                if (parts.Length == 2)
                {
                    canvasList.Add(new CanvasName(parts[0], parts[1]));
                }
                else
                {
                    canvasList.Add(new CanvasName(parts[0])); // 只有名称，显示名与名称相同
                }
            }

            Canvas = canvasList.ToArray();
        }
        else
        {
            Canvas = [new CanvasName("BaseCanvas")];
        }
    }


    /// <summary>
    /// 前台窗口信息
    /// </summary>
    /// <param name="id">窗口唯一标识符</param>
    /// <param name="name">窗口名称</param>
    /// <param name="canvas">Legacy canvas metadata. New v3 layout windows must use only <c>BaseCanvas</c>.</param>
    /// </param>
    /// <param name="isBuiltIn">是否是内置窗口</param>
    internal FrontedWindowInfo(string id, string name, string[]? canvas = null, bool isBuiltIn = false)
    {
        Initialize(id, name, canvas, isBuiltIn);
    }

    /// <summary>
    /// 前台窗口信息
    /// </summary>
    /// <param name="id">窗口唯一标识符</param>
    /// <param name="name">窗口名称</param>
    /// <param name="canvas">Legacy canvas metadata. New v3 layout windows must use only <c>BaseCanvas</c>.</param>
    /// </param>
    public FrontedWindowInfo(string id, string name, string[]? canvas = null)
    {
        Initialize(id, name, canvas, false);
    }

    /// <summary>
    /// 前台窗口信息
    /// </summary>
    /// <param name="id">窗口唯一标识符</param>
    /// <param name="name">窗口名称</param>
    /// <param name="isBuiltIn">是否是内置窗口</param>
    internal FrontedWindowInfo(string id, string name, bool isBuiltIn)
    {
        Initialize(id, name, null, isBuiltIn);
    }

    /// <summary>
    /// 前台窗口信息
    /// </summary>
    /// <param name="id">窗口唯一标识符</param>
    /// <param name="name">窗口名称</param>
    public FrontedWindowInfo(string id, string name)
    {
        Initialize(id, name, null, false);
    }

    /// <summary>
    /// 前台窗口信息
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// 画布集合
    /// </summary>
    public CanvasName[] Canvas { get; private set; } = [];

    /// <summary>
    /// 窗口唯一标识符
    /// </summary>
    public string Id { get; private set; } = Guid.Empty.ToString();

    /// <summary>
    /// 是否是内置窗口
    /// </summary>
    public Type? WindowType { get; internal set; }

    /// <summary>
    /// 是否是内置窗口
    /// </summary>
    public bool IsBuiltIn { get; private set; }
}
