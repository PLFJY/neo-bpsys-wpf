namespace neo_bpsys_wpf.Core.Attributes;

/// <summary>
/// 前台窗口信息
/// </summary>
/// <param name="id">id</param>
/// <param name="name">名称</param>
/// <param name="canvas">画布集合(可选，默认只包含 BaseCanvas)</param>
[AttributeUsage(AttributeTargets.Class)]
public class FrontedWindowInfo(string id, string name, string[]? canvas = null) : Attribute
{

    public string Name { get; } = name;
    
    public string[] Canvas { get; } = canvas?? ["BaseCanvas"];

    public string Id { get; } = id;

    public Type? WindowType { get; internal set; }
}