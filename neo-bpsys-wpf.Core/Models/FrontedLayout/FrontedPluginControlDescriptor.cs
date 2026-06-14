using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using System.Windows;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// 插件前台控件的运行时和设计器元数据。
/// </summary>
/// <typeparam name="TConfig">控件配置类型。</typeparam>
public sealed class FrontedPluginControlDescriptor<TConfig> : IFrontedPluginControlDescriptor
    where TConfig : FrontedControlConfigBase
{
    /// <summary>
    /// 所属插件包 ID。
    /// </summary>
    public required string PackageId { get; init; }

    /// <summary>
    /// 控件类型名称。
    /// </summary>
    public required string ControlTypeName { get; init; }

    /// <inheritdoc />
    public string FullControlType => new FrontedPluginControlType(PackageId, ControlTypeName).ToString();

    /// <summary>
    /// 控件配置类型。
    /// </summary>
    public required Type ConfigType { get; init; }

    /// <summary>
    /// 创建控件实例的工厂方法。
    /// </summary>
    public required Func<string, TConfig, FrontedControlBuildContext, FrameworkElement> CreateControl { get; init; }

    /// <summary>
    /// 创建默认配置的工厂方法（可选）。
    /// </summary>
    public Func<TConfig>? CreateDefaultConfig { get; init; }

    /// <summary>
    /// 控件属性描述列表（可选）。
    /// </summary>
    public IReadOnlyList<FrontedPluginPropertyDescriptor>? Properties { get; init; }

    /// <summary>
    /// 配置验证方法（可选）。
    /// </summary>
    public Func<TConfig, IEnumerable<FrontedLayoutValidationMessage>>? Validate { get; init; }

    /// <summary>
    /// 显示名称的本地化键（可选）。
    /// </summary>
    public string? DisplayNameKey { get; init; }

    /// <summary>
    /// 描述的本地化键（可选）。
    /// </summary>
    public string? DescriptionKey { get; init; }

    /// <summary>
    /// 图标标识（可选）。
    /// </summary>
    public string? Icon { get; init; }

    /// <summary>
    /// 最低宿主版本要求（可选）。
    /// </summary>
    public Version? MinHostVersion { get; init; }

    /// <summary>
    /// 配置架构版本。
    /// </summary>
    public int ConfigSchemaVersion { get; init; } = 1;
}
