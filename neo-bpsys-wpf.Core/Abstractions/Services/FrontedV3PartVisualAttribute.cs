namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 标注控件上返回固定 Part Visual 的属性，用于 C# 代码声明 Part Visual。
/// </summary>
/// <remarks>
/// <para>
/// 与 XAML 附加属性 <c>fronted:FrontedV3.PartId</c> 等价，用于纯 C# 控件或在代码中
/// 声明 Part Visual。两种声明方式解析后映射到同一个 <c>FrontedV3Part</c>。
/// </para>
/// <para>
/// 典型用法：
/// </para>
/// <code>
/// [FrontedV3PartVisual("Logo")]
/// public FrameworkElement LogoElement => _logoBorder;
/// </code>
/// <para>
/// 框架在注册控件时通过反射发现标注了该特性的 <c>public</c> 属性，并按
/// <see cref="PartId"/> 匹配到对应的 <c>FrontedV3Part</c> 声明。
/// </para>
/// <para>
/// 缺失或重复的 Visual 诊断：
/// <list type="bullet">
/// <item>声明了 Part 但未找到对应 Visual → 输出 warning，不崩溃 Designer。</item>
/// <item>多个 Visual 映射到同一个 PartId → 输出 warning，使用第一个匹配项。</item>
/// </list>
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class FrontedV3PartVisualAttribute : Attribute
{
    /// <summary>
    /// 初始化 <see cref="FrontedV3PartVisualAttribute"/>。
    /// </summary>
    /// <param name="partId">Part 标识，必须与 <c>FrontedV3Part.Register&lt;TControl&gt;</c> 中的 id 一致。</param>
    public FrontedV3PartVisualAttribute(string partId)
    {
        PartId = partId ?? throw new ArgumentNullException(nameof(partId));
    }

    /// <summary>
    /// 获取该属性对应的 Part 标识。
    /// </summary>
    public string PartId { get; }
}
