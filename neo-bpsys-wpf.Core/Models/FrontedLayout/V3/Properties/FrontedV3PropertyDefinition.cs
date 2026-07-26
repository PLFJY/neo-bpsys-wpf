using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties;

/// <summary>
/// v3 前台控件属性的已解析定义，由 <see cref="FrontedV3Property"/> 在注册时转换而来。
/// </summary>
/// <remarks>
/// <para>
/// 该类型是属性的"运行时定义"，同时携带 <see cref="OptionsPath"/>（逻辑路径）、
/// <see cref="Storage"/>（存储访问器）、<see cref="PropertyType"/>（强类型）和 <see cref="Metadata"/>（元数据）。
/// </para>
/// <para>
/// <see cref="GetValue"/> / <see cref="SetValue"/> 是 Options 视图与 Designer 属性网格的唯一读写入口：
/// 读取时调用 <see cref="IFrontedV3StorageAccessor.GetValue"/> 取原始值，再通过
/// <c>FrontedV3ValueConverter</c> 转换为 <see cref="PropertyType"/>；写入时反向转换后调用
/// <see cref="IFrontedV3StorageAccessor.SetValue"/>。Options 视图不缓存独立值。
/// </para>
/// </remarks>
public sealed class FrontedV3PropertyDefinition
{
    /// <summary>
    /// 初始化 <see cref="FrontedV3PropertyDefinition"/>。
    /// </summary>
    /// <param name="optionsPath">属性在 Options 视图中的逻辑路径。</param>
    /// <param name="storage">属性的存储访问器。</param>
    /// <param name="propertyType">属性的强类型。</param>
    /// <param name="metadata">属性元数据。</param>
    public FrontedV3PropertyDefinition(
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
    /// 从给定 <paramref name="config"/> 读取属性值并转换为 <see cref="PropertyType"/>。
    /// </summary>
    /// <param name="config">控件配置实例。</param>
    /// <returns>转换后的属性值。</returns>
    public object? GetValue(FrontedControlConfigBase config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var raw = Storage.GetValue(config);
        return FrontedV3ValueConverter.Convert(raw, PropertyType);
    }

    /// <summary>
    /// 将值转换为 <see cref="PropertyType"/> 后写入 <paramref name="config"/>。
    /// </summary>
    /// <param name="config">控件配置实例。</param>
    /// <param name="value">要写入的值。</param>
    public void SetValue(FrontedControlConfigBase config, object? value)
    {
        ArgumentNullException.ThrowIfNull(config);
        var converted = FrontedV3ValueConverter.Convert(value, PropertyType);
        Storage.SetValue(config, converted);
    }
}
