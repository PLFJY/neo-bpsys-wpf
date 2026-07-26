using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties;

/// <summary>
/// v3 前台控件的强类型属性声明，作为控件类上的 <c>public static readonly</c> 字段使用。
/// </summary>
/// <typeparam name="T">属性值的运行时类型。</typeparam>
/// <remarks>
/// <para>
/// 典型用法：
/// </para>
/// <code>
/// public static readonly FrontedV3Property&lt;string&gt; TextColorProperty =
///     new("Appearance.TextColor", FrontedV3Storage.ExtensionData("TextColor"));
/// </code>
/// <para>
/// <see cref="FrontedV3Property.OptionsPath"/> 只是 Designer 属性网格与 StyleTransfer 的逻辑路径，不进入 JSON；
/// 实际读写位置由 <see cref="FrontedV3Property.Storage"/> 决定。
/// </para>
/// </remarks>
public sealed class FrontedV3Property<T> : FrontedV3Property
{
    /// <summary>
    /// 初始化 <see cref="FrontedV3Property{T}"/>。
    /// </summary>
    /// <param name="optionsPath">属性在 Options 视图中的逻辑路径，例如 <c>Appearance.TextColor</c>。</param>
    /// <param name="storage">属性的存储访问器。</param>
    /// <param name="metadata">属性元数据；为 <see langword="null"/> 时使用默认元数据。</param>
    public FrontedV3Property(
        string optionsPath,
        IFrontedV3StorageAccessor storage,
        FrontedV3PropertyMetadata? metadata = null)
        : base(optionsPath, storage, typeof(T), metadata ?? new FrontedV3PropertyMetadata())
    {
    }
}
