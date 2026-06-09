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
    /// <param name="id">窗口唯一标识符</param>
    /// <param name="name">窗口名称</param>
    /// <param name="isBuiltIn">是否是内置窗口</param>
    internal FrontedWindowInfo(string id, string name, bool isBuiltIn)
    {
        Name = name;
        Id = id;
        IsBuiltIn = isBuiltIn;
    }

    /// <summary>
    /// 前台窗口信息
    /// </summary>
    /// <param name="id">窗口唯一标识符</param>
    /// <param name="name">窗口名称</param>
    public FrontedWindowInfo(string id, string name)
    {
        Name = name;
        Id = id;
    }

    /// <summary>
    /// 前台窗口信息
    /// </summary>
    /// <param name="id">窗口唯一标识符</param>
    /// <param name="name">窗口名称</param>
    /// <param name="canvas">已忽略。Canvas 注册不再受支持；每个前台窗口只有一个内部 BaseCanvas。</param>
    /// <param name="isBuiltIn">是否是内置窗口</param>
    [Obsolete("Canvas registration is no longer supported. The canvas parameter is ignored; every fronted window has exactly one internal BaseCanvas.")]
    internal FrontedWindowInfo(string id, string name, string[]? canvas, bool isBuiltIn)
        : this(id, name, isBuiltIn)
    {
    }

    /// <summary>
    /// 前台窗口信息
    /// </summary>
    /// <param name="id">窗口唯一标识符</param>
    /// <param name="name">窗口名称</param>
    /// <param name="canvas">已忽略。Canvas 注册不再受支持；每个前台窗口只有一个内部 BaseCanvas。</param>
    [Obsolete("Canvas registration is no longer supported. The canvas parameter is ignored; every fronted window has exactly one internal BaseCanvas.")]
    public FrontedWindowInfo(string id, string name, string[]? canvas)
        : this(id, name)
    {
    }

    /// <summary>
    /// 前台窗口信息
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// 窗口唯一标识符
    /// </summary>
    public string Id { get; private set; } = Guid.Empty.ToString();

    /// <summary>
    /// 窗口 CLR 类型，在注册时由扩展方法设置。
    /// </summary>
    public Type? WindowType { get; internal set; }

    /// <summary>
    /// 是否是内置窗口
    /// </summary>
    public bool IsBuiltIn { get; private set; }
}
