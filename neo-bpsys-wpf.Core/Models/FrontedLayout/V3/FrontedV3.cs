using System.Windows;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.V3;

/// <summary>
/// v3 前台控件的 XAML 附加属性入口，提供 <see cref="PartIdProperty"/> 用于在 XAML 中标记 Part Visual。
/// </summary>
/// <remarks>
/// <para>
/// XAML 用法：
/// </para>
/// <code>
/// &lt;Border fronted:FrontedV3.PartId="Logo"&gt;
///     &lt;Image /&gt;
/// &lt;/Border&gt;
/// </code>
/// <para>
/// 与 C# 特性 <c>[FrontedV3PartVisual("Logo")]</c> 等价，两种声明方式解析后映射到同一个 Part。
/// </para>
/// <para>
/// Visual 发现器（<c>FrontedV3PartVisualResolver</c>）同时扫描 XAML 附加属性与 C# 特性，
/// 缺失或重复的 Visual 输出诊断日志，不崩溃 Designer。
/// </para>
/// </remarks>
public static class FrontedV3
{
    /// <summary>
    /// 标识 <see cref="PartIdProperty"/> 附加属性。
    /// </summary>
    public static readonly DependencyProperty PartIdProperty = DependencyProperty.RegisterAttached(
        "PartId",
        typeof(string),
        typeof(FrontedV3),
        new PropertyMetadata(null));

    /// <summary>
    /// 从指定对象读取 <c>PartId</c> 附加属性值。
    /// </summary>
    /// <param name="obj">要读取附加属性的对象。</param>
    /// <returns>Part 标识；未设置时为 <see langword="null"/>。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="obj"/> 为 <see langword="null"/> 时抛出。</exception>
    public static string? GetPartId(DependencyObject obj)
    {
        ArgumentNullException.ThrowIfNull(obj);
        return (string?)obj.GetValue(PartIdProperty);
    }

    /// <summary>
    /// 将 <c>PartId</c> 附加属性值设置到指定对象。
    /// </summary>
    /// <param name="obj">要设置附加属性的对象。</param>
    /// <param name="value">Part 标识。</param>
    /// <exception cref="ArgumentNullException">当 <paramref name="obj"/> 为 <see langword="null"/> 时抛出。</exception>
    public static void SetPartId(DependencyObject obj, string? value)
    {
        ArgumentNullException.ThrowIfNull(obj);
        obj.SetValue(PartIdProperty, value);
    }
}
