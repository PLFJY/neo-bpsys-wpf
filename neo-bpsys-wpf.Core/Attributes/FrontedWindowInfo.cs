namespace neo_bpsys_wpf.Core.Attributes;

/// <summary>
/// 前台窗口信息
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class FrontedWindowInfo : Attribute
{
    /// <summary>
    /// 前台窗口信息
    /// </summary>
    /// <param name="id">id</param>
    /// <param name="name">名称</param>
    /// <param name="canvas">画布集合(默认只包含 BaseCanvas)</param>
    /// <param name="isBuiltIn">是否是内置窗口 (强烈建议插件不要设置它为 true，因为这会影响进入前台窗口管理)</param>
    public FrontedWindowInfo(string id, string name, string[]? canvas = null, bool isBuiltIn = false)
    {
        Name = name;
        Canvas = canvas?? ["BaseCanvas"];
        Id = id;
        IsBuiltIn = isBuiltIn;
    }
    
    /// <summary>
    /// 前台窗口信息
    /// </summary>
    /// <param name="id">id</param>
    /// <param name="name">名称</param>
    /// <param name="isBuiltIn">是否是内置窗口 (强烈建议插件不要设置它为 true，因为这会影响进入前台窗口管理)</param>
    public FrontedWindowInfo(string id, string name, bool isBuiltIn)
    {
        Name = name;
        Canvas = ["BaseCanvas"];
        Id = id;
        IsBuiltIn = isBuiltIn;
    }
    
    /// <summary>
    /// 前台窗口信息
    /// </summary>
    /// <param name="id">id</param>
    /// <param name="name">名称</param>
    public FrontedWindowInfo(string id, string name)
    {
        Name = name;
        Canvas = ["BaseCanvas"];
        Id = id;
    }

    public string Name { get; }

    public string[] Canvas { get; }

    public string Id { get; }

    public Type? WindowType { get; internal set; }

    /// <summary>
    /// 是否是内置窗口
    /// </summary>
    public bool IsBuiltIn { get; }
}
