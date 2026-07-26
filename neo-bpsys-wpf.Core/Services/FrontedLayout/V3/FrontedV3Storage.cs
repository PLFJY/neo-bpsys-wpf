using System.Collections;
using System.Reflection;
using System.Text.Json;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout.V3;

/// <summary>
/// v3 前台控件属性存储访问器的工厂，提供读写 <see cref="PluginFrontedControlConfig.ExtensionData"/>、
/// 内置 CLR 属性与集合项属性的三种实现。
/// </summary>
/// <remarks>
/// <para>
/// 插件控件通常使用 <see cref="ExtensionData(string)"/>，将属性值存储到
/// <see cref="PluginFrontedControlConfig.ExtensionData"/> 字典，序列化后平铺到 JSON 根级。
/// 内置控件迁移到 v3 API 后使用 <see cref="ClrProperty(string)"/>，直接反射读写 Config 的 CLR 属性。
/// </para>
/// <para>
/// Phase 4 新增 <see cref="CollectionItemProperty"/>，用于读写 PartCollection 集合项上的 CLR 属性
/// （如 GlobalScoreRow.Cells 中某个 Cell 的 <c>X</c>/<c>Y</c>/<c>Width</c>/<c>Height</c>），
/// 以及固定 Part 存储在列表中的属性（如 MapV2Display.InternalParts 中某个部件的 <c>X</c>）。
/// </para>
/// </remarks>
public static class FrontedV3Storage
{
    /// <summary>
    /// 创建读写 <see cref="PluginFrontedControlConfig.ExtensionData"/> 的存储访问器。
    /// </summary>
    /// <param name="key">ExtensionData 字典中的键，同时也是序列化后 JSON 根级的字段名。</param>
    /// <returns>读写 ExtensionData 的 <see cref="IFrontedV3StorageAccessor"/>。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="key"/> 为 <see langword="null"/> 时抛出。</exception>
    public static IFrontedV3StorageAccessor ExtensionData(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return new ExtensionDataStorageAccessor(key);
    }

    /// <summary>
    /// 创建反射读写 Config CLR 属性的存储访问器。
    /// </summary>
    /// <param name="propertyName">Config 上 CLR 属性的名称。</param>
    /// <returns>反射读写 CLR 属性的 <see cref="IFrontedV3StorageAccessor"/>。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="propertyName"/> 为 <see langword="null"/> 时抛出。</exception>
    public static IFrontedV3StorageAccessor ClrProperty(string propertyName)
    {
        ArgumentNullException.ThrowIfNull(propertyName);
        return new ClrPropertyStorageAccessor(propertyName);
    }

    /// <summary>
    /// 创建读写集合项 CLR 属性的存储访问器，通过键定位集合中的特定项。
    /// </summary>
    /// <param name="collectionGetter">从 Config 获取集合列表的函数。</param>
    /// <param name="itemKeySelector">从集合项获取唯一键的函数。</param>
    /// <param name="itemKey">要定位的集合项唯一键。</param>
    /// <param name="propertyName">集合项上 CLR 属性的名称。</param>
    /// <returns>读写集合项 CLR 属性的 <see cref="IFrontedV3StorageAccessor"/>。</returns>
    /// <exception cref="ArgumentNullException">当任一参数为 <see langword="null"/> 时抛出。</exception>
    /// <remarks>
    /// <para>
    /// 该访问器用于 PartCollection 集合项的几何属性读写（如 GlobalScoreRow.Cells 中 Cell 的 <c>X</c>），
    /// 以及固定 Part 存储在列表中的属性读写（如 MapV2Display.InternalParts 中部件的 <c>X</c>）。
    /// </para>
    /// <para>
    /// 访问器在每次读写时通过 <paramref name="itemKeySelector"/> 遍历集合查找匹配项，
    /// 找不到匹配项时读取返回 <see langword="null"/>，写入不执行。
    /// </para>
    /// </remarks>
    public static IFrontedV3StorageAccessor CollectionItemProperty(
        Func<FrontedControlConfigBase, IList> collectionGetter,
        Func<object, string> itemKeySelector,
        string itemKey,
        string propertyName)
    {
        ArgumentNullException.ThrowIfNull(collectionGetter);
        ArgumentNullException.ThrowIfNull(itemKeySelector);
        ArgumentNullException.ThrowIfNull(itemKey);
        ArgumentNullException.ThrowIfNull(propertyName);
        return new CollectionItemStorageAccessor(collectionGetter, itemKeySelector, itemKey, propertyName);
    }

    private sealed class ExtensionDataStorageAccessor : IFrontedV3StorageAccessor
    {
        private readonly string _key;

        public ExtensionDataStorageAccessor(string key)
        {
            _key = key;
        }

        public string TargetField => _key;

        public object? GetValue(FrontedControlConfigBase config)
        {
            if (config is PluginFrontedControlConfig pluginConfig
                && pluginConfig.ExtensionData.TryGetValue(_key, out var element))
            {
                return element;
            }

            return null;
        }

        public void SetValue(FrontedControlConfigBase config, object? value)
        {
            if (config is not PluginFrontedControlConfig pluginConfig)
            {
                return;
            }

            var element = FrontedV3ValueConverter.ToJsonElement(value);
            pluginConfig.ExtensionData[_key] = element;
        }
    }

    private sealed class ClrPropertyStorageAccessor : IFrontedV3StorageAccessor
    {
        private readonly string _propertyName;

        public ClrPropertyStorageAccessor(string propertyName)
        {
            _propertyName = propertyName;
        }

        public string TargetField => _propertyName;

        public object? GetValue(FrontedControlConfigBase config)
        {
            var property = FindProperty(config);
            return property?.GetValue(config);
        }

        public void SetValue(FrontedControlConfigBase config, object? value)
        {
            var property = FindProperty(config);
            if (property is null || !property.CanWrite)
            {
                return;
            }

            var converted = FrontedV3ValueConverter.Convert(value, property.PropertyType);
            property.SetValue(config, converted);
        }

        private PropertyInfo? FindProperty(FrontedControlConfigBase config)
        {
            return config.GetType().GetProperty(
                _propertyName,
                BindingFlags.Instance | BindingFlags.Public);
        }
    }

    private sealed class CollectionItemStorageAccessor : IFrontedV3StorageAccessor
    {
        private readonly Func<FrontedControlConfigBase, IList> _collectionGetter;
        private readonly Func<object, string> _itemKeySelector;
        private readonly string _itemKey;
        private readonly string _propertyName;

        public CollectionItemStorageAccessor(
            Func<FrontedControlConfigBase, IList> collectionGetter,
            Func<object, string> itemKeySelector,
            string itemKey,
            string propertyName)
        {
            _collectionGetter = collectionGetter;
            _itemKeySelector = itemKeySelector;
            _itemKey = itemKey;
            _propertyName = propertyName;
        }

        public string TargetField => _propertyName;

        public object? GetValue(FrontedControlConfigBase config)
        {
            var item = FindItem(config);
            if (item is null)
            {
                return null;
            }

            var property = FindItemProperty(item);
            return property?.GetValue(item);
        }

        public void SetValue(FrontedControlConfigBase config, object? value)
        {
            var item = FindItem(config);
            if (item is null)
            {
                return;
            }

            var property = FindItemProperty(item);
            if (property is null || !property.CanWrite)
            {
                return;
            }

            var converted = FrontedV3ValueConverter.Convert(value, property.PropertyType);
            property.SetValue(item, converted);
        }

        private object? FindItem(FrontedControlConfigBase config)
        {
            var collection = _collectionGetter(config);
            if (collection is null)
            {
                return null;
            }

            foreach (var item in collection)
            {
                if (item is null)
                {
                    continue;
                }

                var key = _itemKeySelector(item);
                if (string.Equals(key, _itemKey, StringComparison.Ordinal))
                {
                    return item;
                }
            }

            return null;
        }

        private PropertyInfo? FindItemProperty(object item)
        {
            return item.GetType().GetProperty(
                _propertyName,
                BindingFlags.Instance | BindingFlags.Public);
        }
    }
}
