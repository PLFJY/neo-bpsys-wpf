using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Parts;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Parts;

/// <summary>
/// v3 前台控件 Part Visual 的运行时绑定器，在控件创建后由 Host 调用，
/// 将 Part Storage 中的几何值（X/Y/Width/Height）应用到通过 <see cref="FrontedV3PartVisualResolver"/>
/// 发现出的 <see cref="FrameworkElement"/> 上。
/// </summary>
/// <remarks>
/// <para>
/// 该绑定器是 Part 系统在 Runtime 的框架级入口，替代插件作者在
/// <c>OnInitializeFrontedV3</c> 中手写几何读取代码的需要。
/// 调用链为：
/// <list type="bullet">
/// <item><see cref="FrontedV3PartVisualResolver.Resolve"/> 发现 PartId → Visual 映射并输出诊断。</item>
/// <item>本绑定器根据 Part 的 <see cref="FrontedV3PartDefinition.WidthStorage"/>/
/// <see cref="FrontedV3PartDefinition.HeightStorage"/>/
/// <see cref="FrontedV3PartDefinition.XStorage"/>/
/// <see cref="FrontedV3PartDefinition.YStorage"/> 读取 Config 中的几何值并应用到对应 Visual。</item>
/// </list>
/// </para>
/// <para>
/// <see cref="FrontedV3PartCapabilities"/> 不影响读取与应用：能力仅约束 Designer 中允许的几何操作类型，
/// Runtime 绑定器无论能力如何都应用 Storage 中已有的值。这是因为绑定器不产生编辑动作，
/// 只是把已持久化的值恢复到视觉。
/// </para>
/// <para>
/// 几何应用规则：
/// <list type="bullet">
/// <item>Width/Height：当 Storage 返回非 null 值时设置 <see cref="FrameworkElement.Width"/>/
/// <see cref="FrameworkElement.Height"/>；为 null 时保留 XAML 中声明的默认值。</item>
/// <item>X/Y：通过 <see cref="Canvas.SetLeft"/>/<see cref="Canvas.SetTop"/> 附加属性设置；
/// 仅当 Visual 的父容器为 <see cref="Canvas"/> 时生效，其他容器中附加属性不会产生视觉效果，
/// 但也不会报错。</item>
/// </list>
/// </para>
/// <para>
/// 缺失或重复 Visual 的诊断由 <see cref="FrontedV3PartVisualResolver"/> 输出，本绑定器原样返回。
/// </para>
/// </remarks>
public static class FrontedV3PartVisualRuntimeBinder
{
    /// <summary>
    /// 从指定控件发现 Part Visual 并将 Storage 中的几何值应用到对应 Visual。
    /// </summary>
    /// <param name="control">刚创建并初始化的 v3 控件实例。</param>
    /// <param name="partDefinitions">该控件声明的 Part 定义列表。</param>
    /// <param name="config">控件配置实例，作为几何值的单一事实来源。</param>
    /// <param name="logger">可选日志，用于记录诊断。</param>
    /// <returns>包含已发现 Visual 与诊断的结果；调用方可据此记录日志或显示 Designer 诊断。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="control"/>、
    /// <paramref name="partDefinitions"/> 或 <paramref name="config"/> 为 <see langword="null"/> 时抛出。</exception>
    /// <remarks>
    /// 该方法不抛出 Part 相关异常：发现失败、Visual 缺失、Storage 读取异常等都输出为诊断或日志，
    /// 不阻止 Host 完成初始化。这确保插件 Part 声明错误不会让整个控件无法显示。
    /// </remarks>
    public static FrontedV3PartVisualDiscoveryResult Bind(
        FrameworkElement control,
        IReadOnlyList<FrontedV3PartDefinition> partDefinitions,
        FrontedControlConfigBase config,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(partDefinitions);
        ArgumentNullException.ThrowIfNull(config);

        var discovery = FrontedV3PartVisualResolver.Resolve(control, partDefinitions, logger);

        foreach (var (partId, visual) in discovery.DiscoveredVisuals)
        {
            var part = TryFindPart(partDefinitions, partId);
            if (part is null)
            {
                continue;
            }

            ApplyGeometryToVisual(visual, part, config, logger);
        }

        return discovery;
    }

    private static FrontedV3PartDefinition? TryFindPart(
        IReadOnlyList<FrontedV3PartDefinition> partDefinitions,
        string partId)
    {
        foreach (var part in partDefinitions)
        {
            if (string.Equals(part.Id, partId, StringComparison.Ordinal))
            {
                return part;
            }
        }

        return null;
    }

    private static void ApplyGeometryToVisual(
        FrameworkElement visual,
        FrontedV3PartDefinition part,
        FrontedControlConfigBase config,
        ILogger? logger)
    {
        if (part.WidthStorage is not null)
        {
            var width = ReadNullableDouble(part.WidthStorage, config);
            if (width.HasValue)
            {
                visual.Width = width.Value;
            }
        }

        if (part.HeightStorage is not null)
        {
            var height = ReadNullableDouble(part.HeightStorage, config);
            if (height.HasValue)
            {
                visual.Height = height.Value;
            }
        }

        if (part.XStorage is not null)
        {
            var x = ReadDouble(part.XStorage, config, 0D);
            Canvas.SetLeft(visual, x);
        }

        if (part.YStorage is not null)
        {
            var y = ReadDouble(part.YStorage, config, 0D);
            Canvas.SetTop(visual, y);
        }

        logger?.LogDebug(
            "Applied part geometry to visual {VisualType} for PartId {PartId}.",
            visual.GetType().FullName,
            part.Id);
    }

    private static double ReadDouble(
        IFrontedV3StorageAccessor? storage,
        FrontedControlConfigBase config,
        double defaultValue)
    {
        if (storage is null)
        {
            return defaultValue;
        }

        var value = storage.GetValue(config);
        if (value is null)
        {
            return defaultValue;
        }

        if (value is double d)
        {
            return d;
        }

        // ExtensionData 存储返回 JsonElement，需单独处理（JsonElement 不实现 IConvertible）。
        if (value is JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == JsonValueKind.Number
                && jsonElement.TryGetDouble(out var jsonDouble))
            {
                return jsonDouble;
            }

            return defaultValue;
        }

        if (value is IConvertible convertible)
        {
            try
            {
                return System.Convert.ToDouble(convertible, CultureInfo.InvariantCulture);
            }
            catch
            {
                return defaultValue;
            }
        }

        return defaultValue;
    }

    private static double? ReadNullableDouble(
        IFrontedV3StorageAccessor? storage,
        FrontedControlConfigBase config)
    {
        if (storage is null)
        {
            return null;
        }

        var value = storage.GetValue(config);
        if (value is null)
        {
            return null;
        }

        if (value is double d)
        {
            return d;
        }

        // ExtensionData 存储返回 JsonElement，需单独处理（JsonElement 不实现 IConvertible）。
        if (value is JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == JsonValueKind.Null
                || jsonElement.ValueKind == JsonValueKind.Undefined)
            {
                return null;
            }

            if (jsonElement.ValueKind == JsonValueKind.Number
                && jsonElement.TryGetDouble(out var jsonDouble))
            {
                return jsonDouble;
            }

            return null;
        }

        if (value is IConvertible convertible)
        {
            try
            {
                return System.Convert.ToDouble(convertible, CultureInfo.InvariantCulture);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }
}
