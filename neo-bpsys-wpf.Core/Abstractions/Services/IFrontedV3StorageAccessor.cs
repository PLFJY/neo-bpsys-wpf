using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// v3 前台控件属性的存储访问器，负责从 <see cref="FrontedControlConfigBase"/>
/// 读取或写入单个属性值。
/// </summary>
/// <remarks>
/// <para>
/// 存储访问器是属性定义（<see cref="neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties.FrontedV3PropertyDefinition"/>）
/// 的一部分，与 <c>OptionsPath</c> 解耦：<c>OptionsPath</c> 是 Designer 属性网格与 StyleTransfer 使用的逻辑路径，
/// 而存储访问器决定值在 Config 上的实际读写位置（根级 CLR 属性或 <see cref="PluginFrontedControlConfig.ExtensionData"/> 字典键）。
/// </para>
/// <para>
/// 存储访问器不得覆盖根级保留字段（<c>Left</c>/<c>Top</c>/<c>Width</c>/<c>Height</c>/<c>ZIndex</c>/
/// <c>Visibility</c>/<c>BehaviorGuid</c>/<c>GaussianBlur</c>/<c>ControlType</c>），该校验在注册时由
/// <see cref="neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties.FrontedV3PropertyDefinition"/> 配合
/// <c>FrontedV3ReservedFields</c> 完成。
/// </para>
/// <para>
/// 实现应返回"原始"存储值（例如 <see cref="PluginFrontedControlConfig.ExtensionData"/> 中的 <see cref="System.Text.Json.JsonElement"/>），
/// 类型到目标 <c>PropertyType</c> 的转换由 <c>FrontedV3ValueConverter</c> 统一处理。
/// </para>
/// </remarks>
public interface IFrontedV3StorageAccessor
{
    /// <summary>
    /// 获取该存储访问器读写的目标字段名，用于保留字段校验。
    /// </summary>
    /// <remarks>
    /// 对于 <c>ExtensionData</c> 存储，该值为字典键；对于 CLR 属性存储，该值为属性名。
    /// 该值不得为 <see cref="neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties.FrontedV3ReservedFields"/> 中列出的保留字段。
    /// </remarks>
    string TargetField { get; }

    /// <summary>
    /// 从给定 <paramref name="config"/> 读取属性值。
    /// </summary>
    /// <param name="config">控件配置实例。</param>
    /// <returns>读取到的原始值；缺失时返回 <see langword="null"/> 或类型默认值。</returns>
    object? GetValue(FrontedControlConfigBase config);

    /// <summary>
    /// 向给定 <paramref name="config"/> 写入属性值。
    /// </summary>
    /// <param name="config">控件配置实例。</param>
    /// <param name="value">要写入的值，由调用方按 <c>PropertyType</c> 转换后再传入。</param>
    void SetValue(FrontedControlConfigBase config, object? value);
}
