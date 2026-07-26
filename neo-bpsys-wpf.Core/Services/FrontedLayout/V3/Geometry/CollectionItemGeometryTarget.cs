using System.Collections;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Parts;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Geometry;

/// <summary>
/// PartCollection 集合项的几何操作目标，将 Designer 的 Move/Resize 通过集合项的 Storage 写入 Config。
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
/// 几何值通过集合项的 CLR 属性（<c>X</c>/<c>Y</c>/<c>Width</c>/<c>Height</c>）读写到 Config 的现有集合字段，
/// 不直接操作 Visual 的尺寸。坐标相对于父 Control，而非 Canvas 绝对坐标。
/// </para>
/// <para>
/// 构造时通过 <see cref="FrontedV3PartCollectionDefinition.CollectionGetter"/> 与
/// <see cref="FrontedV3PartCollectionDefinition.ItemKeySelector"/> 创建绑定到指定项键的存储访问器。
/// </para>
/// </remarks>
public sealed class CollectionItemGeometryTarget : IFrontedV3GeometryTarget
{
    private readonly FrontedV3PartCollectionDefinition _collection;
    private readonly FrontedControlConfigBase _config;
    private readonly string _itemKey;
    private readonly IFrontedV3StorageAccessor _xStorage;
    private readonly IFrontedV3StorageAccessor _yStorage;
    private readonly IFrontedV3StorageAccessor _widthStorage;
    private readonly IFrontedV3StorageAccessor _heightStorage;
    private readonly Action? _onVisualSync;

    /// <summary>
    /// 初始化 <see cref="CollectionItemGeometryTarget"/>。
    /// </summary>
    /// <param name="collection">集合定义，决定可操作的维度与能力约束。</param>
    /// <param name="config">控件配置实例，作为集合项几何字段的单一事实来源。</param>
    /// <param name="itemKey">要操作的集合项唯一键。</param>
    /// <param name="onVisualSync">可选的视觉同步回调，在几何值变更后由调用方触发视觉更新。</param>
    /// <exception cref="ArgumentNullException">当 <paramref name="collection"/>、<paramref name="config"/> 或 <paramref name="itemKey"/> 为 <see langword="null"/> 时抛出。</exception>
    public CollectionItemGeometryTarget(
        FrontedV3PartCollectionDefinition collection,
        FrontedControlConfigBase config,
        string itemKey,
        Action? onVisualSync = null)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(itemKey);

        _collection = collection;
        _config = config;
        _itemKey = itemKey;
        _onVisualSync = onVisualSync;

        _xStorage = FrontedV3Storage.CollectionItemProperty(
            collection.CollectionGetter, collection.ItemKeySelector, itemKey, "X");
        _yStorage = FrontedV3Storage.CollectionItemProperty(
            collection.CollectionGetter, collection.ItemKeySelector, itemKey, "Y");
        _widthStorage = FrontedV3Storage.CollectionItemProperty(
            collection.CollectionGetter, collection.ItemKeySelector, itemKey, "Width");
        _heightStorage = FrontedV3Storage.CollectionItemProperty(
            collection.CollectionGetter, collection.ItemKeySelector, itemKey, "Height");
    }

    /// <summary>
    /// 获取当前集合项的唯一键。
    /// </summary>
    public string ItemKey => _itemKey;

    /// <inheritdoc />
    /// <remarks>
    /// 返回集合项相对于父 Control 的 X 坐标；找不到项时返回 0。
    /// </remarks>
    public double Left => ReadDouble(_xStorage, _config, 0D);

    /// <inheritdoc />
    /// <remarks>
    /// 返回集合项相对于父 Control 的 Y 坐标；找不到项时返回 0。
    /// </remarks>
    public double Top => ReadDouble(_yStorage, _config, 0D);

    /// <inheritdoc />
    /// <remarks>
    /// 返回集合项的显式宽度；找不到项时返回 <see langword="null"/>。
    /// </remarks>
    public double? Width => ReadNullableDouble(_widthStorage, _config);

    /// <inheritdoc />
    /// <remarks>
    /// 返回集合项的显式高度；找不到项时返回 <see langword="null"/>。
    /// </remarks>
    public double? Height => ReadNullableDouble(_heightStorage, _config);

    /// <inheritdoc />
    /// <remarks>
    /// 当 <see cref="FrontedV3PartCapabilities.CanMove"/> 为 <see langword="false"/> 时不执行任何写入。
    /// </remarks>
    public void MoveTo(double left, double top)
    {
        if (!_collection.ItemCapabilities.CanMove)
        {
            return;
        }

        _xStorage.SetValue(_config, left);
        _yStorage.SetValue(_config, top);
        _onVisualSync?.Invoke();
    }

    /// <inheritdoc />
    /// <remarks>
    /// 当 <see cref="FrontedV3PartCapabilities.CanResize"/> 为 <see langword="false"/> 时不执行任何写入。
    /// 当 <see cref="FrontedV3PartCapabilities.CanMove"/> 同时为 <see langword="true"/> 时，
    /// 将 <paramref name="left"/>/<paramref name="top"/> 写入 X/Y 属性。
    /// </remarks>
    public void ResizeTo(double left, double top, double? width, double? height)
    {
        if (!_collection.ItemCapabilities.CanResize)
        {
            return;
        }

        _widthStorage.SetValue(_config, width);
        _heightStorage.SetValue(_config, height);

        if (_collection.ItemCapabilities.CanMove)
        {
            _xStorage.SetValue(_config, left);
            _yStorage.SetValue(_config, top);
        }

        _onVisualSync?.Invoke();
    }

    /// <inheritdoc />
    public void ApplyToVisual()
    {
        _onVisualSync?.Invoke();
    }

    private static double ReadDouble(
        IFrontedV3StorageAccessor storage,
        FrontedControlConfigBase config,
        double defaultValue)
    {
        var value = storage.GetValue(config);
        if (value is null)
        {
            return defaultValue;
        }

        if (value is double d)
        {
            return d;
        }

        if (value is IConvertible convertible)
        {
            try
            {
                return System.Convert.ToDouble(convertible, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                return defaultValue;
            }
        }

        return defaultValue;
    }

    private static double? ReadNullableDouble(
        IFrontedV3StorageAccessor storage,
        FrontedControlConfigBase config)
    {
        var value = storage.GetValue(config);
        if (value is null)
        {
            return null;
        }

        if (value is double d)
        {
            return d;
        }

        if (value is IConvertible convertible)
        {
            try
            {
                return System.Convert.ToDouble(convertible, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }
}
