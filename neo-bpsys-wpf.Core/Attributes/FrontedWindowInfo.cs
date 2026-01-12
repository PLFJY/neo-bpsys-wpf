namespace neo_bpsys_wpf.Core.Attributes;

/// <summary>
/// 画布名称信息
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

    public string Name { get; }
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
    /// <param name="canvas">画布集合(默认只包含 BaseCanvas)
    ///     <para>格式说明:</para>
    ///     <para>- 单纯画布名称: "CanvasName" (显示名称将与画布名称相同)</para>
    ///     <para>- 包含显示名称: "CanvasName|显示名称" (用|分隔画布名称和显示名称)</para>
    ///     <para>示例: ["MapBpCanvas", "BpOverViewCanvas|BP概览", "MapV2Canvas|地图V2"]</para>
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
    /// <param name="canvas">画布集合(默认只包含 BaseCanvas)
    ///     <para>格式说明:</para>
    ///     <para>- 单纯画布名称: "CanvasName" (显示名称将与画布名称相同)</para>
    ///     <para>- 包含显示名称: "CanvasName|显示名称" (用|分隔画布名称和显示名称)</para>
    ///     <para>示例: ["MapBpCanvas", "BpOverViewCanvas|BP概览", "MapV2Canvas|地图V2"]</para>
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

    public string Name { get; private set; } = string.Empty;

    public CanvasName[] Canvas { get; private set; } = [];

    public string Id { get; private set; } = Guid.Empty.ToString();

    public Type? WindowType { get; internal set; }

    /// <summary>
    /// 是否是内置窗口
    /// </summary>
    public bool IsBuiltIn { get; private set; }
}