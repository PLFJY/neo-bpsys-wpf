using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties;

/// <summary>
/// v3 前台控件属性声明的非泛型基类，提供反射发现所需的公共成员。
/// </summary>
/// <remarks>
/// 插件作者应使用泛型 <see cref="FrontedV3Property{T}"/> 声明属性，并将其作为控件类上的
/// <c>public static readonly</c> 字段。宿主在注册控件时通过反射发现这些字段，转换为
/// <see cref="FrontedV3PropertyDefinition"/> 并纳入 <see cref="FrontedV3ControlRegistration"/>。
/// </remarks>
public abstract class FrontedV3Property
{
    /// <summary>
    /// 初始化 <see cref="FrontedV3Property"/>。
    /// </summary>
    /// <param name="optionsPath">属性在 Options 视图中的逻辑路径，例如 <c>Appearance.TextColor</c>。</param>
    /// <param name="storage">属性的存储访问器。</param>
    /// <param name="propertyType">属性的强类型。</param>
    /// <param name="metadata">属性元数据。</param>
    protected FrontedV3Property(
        string optionsPath,
        IFrontedV3StorageAccessor storage,
        Type propertyType,
        FrontedV3PropertyMetadata metadata)
    {
        OptionsPath = optionsPath ?? throw new ArgumentNullException(nameof(optionsPath));
        Storage = storage ?? throw new ArgumentNullException(nameof(storage));
        PropertyType = propertyType ?? throw new ArgumentNullException(nameof(propertyType));
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
    }

    /// <summary>
    /// 属性在 Options 视图中的逻辑路径，例如 <c>Appearance.TextColor</c>。
    /// </summary>
    /// <remarks>
    /// 该路径只是 Designer 属性网格与 StyleTransfer 的逻辑键，<b>不</b>直接作为 JSON path 使用，
    /// 也<b>不</b>进入 JSON。实际读写位置由 <see cref="Storage"/> 决定。
    /// </remarks>
    public string OptionsPath { get; }

    /// <summary>
    /// 属性的存储访问器，决定值在 Config 上的实际读写位置。
    /// </summary>
    public IFrontedV3StorageAccessor Storage { get; }

    /// <summary>
    /// 属性的强类型。
    /// </summary>
    public Type PropertyType { get; }

    /// <summary>
    /// 属性元数据。
    /// </summary>
    public FrontedV3PropertyMetadata Metadata { get; }

    /// <summary>
    /// 将该属性声明转换为 <see cref="FrontedV3PropertyDefinition"/>。
    /// </summary>
    /// <returns>与该声明等价的 <see cref="FrontedV3PropertyDefinition"/>。</returns>
    public FrontedV3PropertyDefinition ToDefinition() => new(OptionsPath, Storage, PropertyType, Metadata);
}
