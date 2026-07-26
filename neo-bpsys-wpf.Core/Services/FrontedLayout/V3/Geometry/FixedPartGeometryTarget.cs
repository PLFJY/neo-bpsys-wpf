using System.Globalization;
using System.Text.Json;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Parts;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Geometry;

/// <summary>
/// 固定 Part 的几何操作目标，将 Designer 的 Move/Resize 通过 Part 的 Storage 写入 Config。
/// </summary>
/// <remarks>
/// <para>
/// 该实现遵守 <see cref="FrontedV3PartCapabilities"/> 约束：
/// <list type="bullet">
/// <item><see cref="FrontedV3PartCapabilities.CanMove"/> 为 <see langword="false"/> 时 <see cref="MoveTo"/> 不写入。</item>
/// <item><see cref="FrontedV3PartCapabilities.CanResize"/> 为 <see langword="false"/> 时 <see cref="ResizeTo"/> 不写入。</item>
/// </list>
/// </para>
/// <para>
/// 几何值通过 Part 的 <see cref="FrontedV3PartDefinition.WidthStorage"/>/<see cref="FrontedV3PartDefinition.HeightStorage"/>/
/// <see cref="FrontedV3PartDefinition.XStorage"/>/<see cref="FrontedV3PartDefinition.YStorage"/> 读写到 Config，
/// 不直接操作 Visual 的 Width/Height。存储访问器为 <see langword="null"/> 时该维度不持久化。
/// </para>
/// <para>
/// 坐标相对于父 Control：<see cref="Left"/>/<see cref="Top"/> 返回 Part 相对于父 Control 的坐标，
/// 而非 Canvas 绝对坐标。
/// </para>
/// </remarks>
public sealed class FixedPartGeometryTarget : IFrontedV3GeometryTarget
{
    private readonly FrontedV3PartDefinition _part;
    private readonly FrontedControlConfigBase _config;
    private readonly Action? _onVisualSync;

    /// <summary>
    /// 初始化 <see cref="FixedPartGeometryTarget"/>。
    /// </summary>
    /// <param name="part">Part 定义，决定可操作的维度与能力约束。</param>
    /// <param name="config">控件配置实例，作为 Part 几何字段的单一事实来源。</param>
    /// <param name="onVisualSync">可选的视觉同步回调，在几何值变更后由调用方触发视觉更新。</param>
    /// <exception cref="ArgumentNullException">当 <paramref name="part"/> 或 <paramref name="config"/> 为 <see langword="null"/> 时抛出。</exception>
    public FixedPartGeometryTarget(
        FrontedV3PartDefinition part,
        FrontedControlConfigBase config,
        Action? onVisualSync = null)
    {
        ArgumentNullException.ThrowIfNull(part);
        ArgumentNullException.ThrowIfNull(config);
        _part = part;
        _config = config;
        _onVisualSync = onVisualSync;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 返回 Part 相对于父 Control 的 X 坐标；当 Part 无 X 存储时返回 0。
    /// </remarks>
    public double Left => ReadDouble(_part.XStorage, _config, 0D);

    /// <inheritdoc />
    /// <remarks>
    /// 返回 Part 相对于父 Control 的 Y 坐标；当 Part 无 Y 存储时返回 0。
    /// </remarks>
    public double Top => ReadDouble(_part.YStorage, _config, 0D);

    /// <inheritdoc />
    /// <remarks>
    /// 返回 Part 的显式宽度；当 Part 无宽度存储时返回 <see langword="null"/>。
    /// </remarks>
    public double? Width => ReadNullableDouble(_part.WidthStorage, _config);

    /// <inheritdoc />
    /// <remarks>
    /// 返回 Part 的显式高度；当 Part 无高度存储时返回 <see langword="null"/>。
    /// </remarks>
    public double? Height => ReadNullableDouble(_part.HeightStorage, _config);

    /// <inheritdoc />
    /// <remarks>
    /// 当 <see cref="FrontedV3PartCapabilities.CanMove"/> 为 <see langword="false"/> 时不执行任何写入。
    /// 当 Part 无 X/Y 存储访问器时跳过对应维度。
    /// </remarks>
    public void MoveTo(double left, double top)
    {
        if (!_part.Capabilities.CanMove)
        {
            return;
        }

        if (_part.XStorage is not null)
        {
            _part.XStorage.SetValue(_config, left);
        }

        if (_part.YStorage is not null)
        {
            _part.YStorage.SetValue(_config, top);
        }

        _onVisualSync?.Invoke();
    }

    /// <inheritdoc />
    /// <remarks>
    /// 当 <see cref="FrontedV3PartCapabilities.CanResize"/> 为 <see langword="false"/> 时不执行任何写入。
    /// 当 Part 无宽度/高度存储访问器时跳过对应维度。
    /// 当 <see cref="FrontedV3PartCapabilities.CanMove"/> 同时为 <see langword="true"/> 时，
    /// 将 <paramref name="left"/>/<paramref name="top"/> 写入 X/Y 存储。
    /// </remarks>
    public void ResizeTo(double left, double top, double? width, double? height)
    {
        if (!_part.Capabilities.CanResize)
        {
            return;
        }

        if (_part.WidthStorage is not null)
        {
            _part.WidthStorage.SetValue(_config, width);
        }

        if (_part.HeightStorage is not null)
        {
            _part.HeightStorage.SetValue(_config, height);
        }

        if (_part.Capabilities.CanMove)
        {
            if (_part.XStorage is not null)
            {
                _part.XStorage.SetValue(_config, left);
            }

            if (_part.YStorage is not null)
            {
                _part.YStorage.SetValue(_config, top);
            }
        }

        _onVisualSync?.Invoke();
    }

    /// <inheritdoc />
    public void ApplyToVisual()
    {
        _onVisualSync?.Invoke();
    }

    /// <summary>
    /// 将 Part 几何限制在父 Control 的边界内。
    /// </summary>
    /// <param name="geometry">待限制的 Part 几何。</param>
    /// <param name="parentWidth">父 Control 的宽度。</param>
    /// <param name="parentHeight">父 Control 的高度。</param>
    /// <returns>限制后的 Part 几何；当 <paramref name="parentWidth"/> 或 <paramref name="parentHeight"/> 非正时不限制对应维度。</returns>
    /// <remarks>
    /// 该方法不修改 <paramref name="geometry"/>，而是返回一个新的 <see cref="FrontedV3PartGeometry"/>。
    /// 约束规则：
    /// <list type="bullet">
    /// <item>X 不小于 0，不大于 <c>parentWidth - width</c>（当 width 有值时）。</item>
    /// <item>Y 不小于 0，不大于 <c>parentHeight - height</c>（当 height 有值时）。</item>
    /// <item>Width 不大于 <c>parentWidth - X</c>。</item>
    /// <item>Height 不大于 <c>parentHeight - Y</c>。</item>
    /// </list>
    /// </remarks>
    public static FrontedV3PartGeometry ClampToParent(
        FrontedV3PartGeometry geometry,
        double parentWidth,
        double parentHeight)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        var x = geometry.X;
        var y = geometry.Y;
        var width = geometry.Width;
        var height = geometry.Height;

        if (parentWidth > 0)
        {
            if (width.HasValue && width.Value > parentWidth)
            {
                width = parentWidth;
            }

            var maxX = width.HasValue ? parentWidth - width.Value : parentWidth;
            if (maxX < 0)
            {
                maxX = 0;
            }

            if (x < 0)
            {
                x = 0;
            }
            else if (x > maxX)
            {
                x = maxX;
            }
        }

        if (parentHeight > 0)
        {
            if (height.HasValue && height.Value > parentHeight)
            {
                height = parentHeight;
            }

            var maxY = height.HasValue ? parentHeight - height.Value : parentHeight;
            if (maxY < 0)
            {
                maxY = 0;
            }

            if (y < 0)
            {
                y = 0;
            }
            else if (y > maxY)
            {
                y = maxY;
            }
        }

        return new FrontedV3PartGeometry(x, y, width, height);
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
