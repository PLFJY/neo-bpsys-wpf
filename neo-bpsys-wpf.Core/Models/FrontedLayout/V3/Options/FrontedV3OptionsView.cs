using System.ComponentModel;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Options;

/// <summary>
/// v3 前台控件 Options 的动态代理视图，按属性 Schema 将逻辑路径（如 <c>Appearance.TextColor</c>）
/// 投影到当前 <see cref="FrontedControlConfigBase"/> 的实际存储位置。
/// </summary>
/// <remarks>
/// <para>
/// Options 视图是运行时与 Designer 的属性投影，<b>不进入 JSON</b>，<b>不缓存独立值</b>：
/// 读取属性时调用 <see cref="FrontedV3PropertyDefinition.GetValue"/>；
/// 修改属性时调用 <see cref="FrontedV3PropertyDefinition.SetValue"/> 并触发
/// <see cref="PropertyChanged"/>，使 WPF 绑定立即更新视觉。
/// </para>
/// <para>
/// 视图按 <see cref="FrontedV3PropertyDefinition.OptionsPath"/> 的段分层组织：
/// <c>Appearance.TextColor</c> 在根视图暴露 <c>Appearance</c> 子视图，子视图再暴露 <c>TextColor</c> 叶子属性。
/// WPF 绑定 <c>{Binding Appearance.TextColor}</c> 通过 <see cref="ICustomTypeDescriptor"/> 发现这些动态属性。
/// </para>
/// <para>
/// 单一事实来源：Options 的任何属性都直接映射到当前 Config 的根级字段，Options 不保存副本。
/// </para>
/// </remarks>
public sealed class FrontedV3OptionsView : INotifyPropertyChanged, ICustomTypeDescriptor
{
    private readonly FrontedControlConfigBase _config;
    private readonly IReadOnlyList<FrontedV3PropertyDefinition> _properties;
    private readonly string _pathPrefix;
    private readonly FrontedV3OptionsView _root;
    private Dictionary<string, FrontedV3OptionsView>? _subViews;

    private FrontedV3OptionsView(
        FrontedControlConfigBase config,
        IReadOnlyList<FrontedV3PropertyDefinition> properties,
        string pathPrefix,
        FrontedV3OptionsView root)
    {
        _config = config;
        _properties = properties;
        _pathPrefix = pathPrefix;
        _root = root;
    }

    /// <summary>
    /// 创建根 Options 视图。
    /// </summary>
    /// <param name="config">控件配置实例，作为属性读写的单一事实来源。</param>
    /// <param name="properties">控件属性定义列表。</param>
    /// <returns>代理 <paramref name="config"/> 的根 Options 视图。</returns>
    /// <exception cref="ArgumentNullException">当参数为 <see langword="null"/> 时抛出。</exception>
    public static FrontedV3OptionsView Create(
        FrontedControlConfigBase config,
        IReadOnlyList<FrontedV3PropertyDefinition> properties)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(properties);
        var view = new FrontedV3OptionsView(config, properties, string.Empty, null!);
        // root 指向自身
        return new FrontedV3OptionsView(config, properties, string.Empty, view);
    }

    /// <summary>
    /// 属性变更事件。叶子属性被修改时，所属子视图与根视图均会触发。
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 通知某个叶子属性已变更，触发 <see cref="PropertyChanged"/>。
    /// </summary>
    /// <param name="leafName">变更的叶子属性名（相对当前视图的段名）。</param>
    internal void NotifyLeafChanged(string leafName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(leafName));
        if (!ReferenceEquals(_root, this))
        {
            _root.RaiseAllPropertiesChanged();
        }
    }

    private void RaiseAllPropertiesChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    private string GetPrefix()
    {
        return _pathPrefix.Length == 0 ? string.Empty : _pathPrefix + ".";
    }

    private List<OptionsChild> GetChildren()
    {
        var children = new List<OptionsChild>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var prefix = GetPrefix();

        foreach (var prop in _properties)
        {
            if (!prop.OptionsPath.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var relative = prefix.Length == 0 ? prop.OptionsPath : prop.OptionsPath[prefix.Length..];
            var dotIndex = relative.IndexOf('.');
            var firstSegment = dotIndex < 0 ? relative : relative[..dotIndex];

            if (string.IsNullOrEmpty(firstSegment) || !seen.Add(firstSegment))
            {
                continue;
            }

            var isLeaf = dotIndex < 0;
            if (isLeaf)
            {
                children.Add(new OptionsChild(firstSegment, prop, null));
            }
            else
            {
                var subPrefix = _pathPrefix.Length == 0 ? firstSegment : _pathPrefix + "." + firstSegment;
                var subView = GetOrCreateSubView(firstSegment, subPrefix);
                children.Add(new OptionsChild(firstSegment, null, subView));
            }
        }

        return children;
    }

    private FrontedV3OptionsView GetOrCreateSubView(string segment, string subPrefix)
    {
        _subViews ??= new Dictionary<string, FrontedV3OptionsView>(StringComparer.Ordinal);
        if (!_subViews.TryGetValue(segment, out var sub))
        {
            sub = new FrontedV3OptionsView(_config, _properties, subPrefix, _root);
            _subViews[segment] = sub;
        }
        return sub;
    }

    /// <inheritdoc />
    public PropertyDescriptorCollection GetProperties()
    {
        return GetProperties(null);
    }

    /// <inheritdoc />
    public PropertyDescriptorCollection GetProperties(Attribute[]? attributes)
    {
        var children = GetChildren();
        var descriptors = new PropertyDescriptor[children.Count];
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            descriptors[i] = child.IsLeaf
                ? new OptionsLeafPropertyDescriptor(child.Segment, _config, child.Leaf!, this)
                : new OptionsGroupPropertyDescriptor(child.Segment, child.SubView!);
        }
        return new PropertyDescriptorCollection(descriptors, readOnly: true);
    }

    /// <inheritdoc />
    public string GetClassName()
    {
        return typeof(FrontedV3OptionsView).Name;
    }

    /// <inheritdoc />
    public string? GetComponentName()
    {
        return null;
    }

    /// <inheritdoc />
    public AttributeCollection GetAttributes()
    {
        return TypeDescriptor.GetAttributes(typeof(FrontedV3OptionsView), true);
    }

    /// <inheritdoc />
    public TypeConverter GetConverter()
    {
        return TypeDescriptor.GetConverter(typeof(FrontedV3OptionsView), true);
    }

    /// <inheritdoc />
    public EventDescriptor? GetDefaultEvent()
    {
        return null;
    }

    /// <inheritdoc />
    public PropertyDescriptor? GetDefaultProperty()
    {
        return null;
    }

    /// <inheritdoc />
    public object? GetEditor(Type editorBaseType)
    {
        return TypeDescriptor.GetEditor(typeof(FrontedV3OptionsView), editorBaseType, true);
    }

    /// <inheritdoc />
    public EventDescriptorCollection GetEvents()
    {
        return EventDescriptorCollection.Empty;
    }

    /// <inheritdoc />
    public EventDescriptorCollection GetEvents(Attribute[]? attributes)
    {
        return EventDescriptorCollection.Empty;
    }

    /// <inheritdoc />
    public object GetPropertyOwner(PropertyDescriptor? pd)
    {
        return this;
    }

    private readonly struct OptionsChild
    {
        public OptionsChild(string segment, FrontedV3PropertyDefinition? leaf, FrontedV3OptionsView? subView)
        {
            Segment = segment;
            Leaf = leaf;
            SubView = subView;
        }

        public string Segment { get; }

        public FrontedV3PropertyDefinition? Leaf { get; }

        public FrontedV3OptionsView? SubView { get; }

        public bool IsLeaf => Leaf is not null;
    }

    private sealed class OptionsLeafPropertyDescriptor : PropertyDescriptor
    {
        private readonly FrontedControlConfigBase _config;
        private readonly FrontedV3PropertyDefinition _definition;
        private readonly FrontedV3OptionsView _owner;

        public OptionsLeafPropertyDescriptor(
            string name,
            FrontedControlConfigBase config,
            FrontedV3PropertyDefinition definition,
            FrontedV3OptionsView owner)
            : base(name, null)
        {
            _config = config;
            _definition = definition;
            _owner = owner;
        }

        public override Type ComponentType => typeof(FrontedV3OptionsView);

        public override Type PropertyType => _definition.PropertyType;

        public override bool IsReadOnly => _definition.Metadata.IsReadOnly;

        public override bool CanResetValue(object component) => false;

        public override void ResetValue(object component)
        {
        }

        public override object? GetValue(object? component)
        {
            return _definition.GetValue(_config);
        }

        public override void SetValue(object? component, object? value)
        {
            _definition.SetValue(_config, value);
            _owner.NotifyLeafChanged(Name);
        }

        public override bool ShouldSerializeValue(object component) => false;
    }

    private sealed class OptionsGroupPropertyDescriptor : PropertyDescriptor
    {
        private readonly FrontedV3OptionsView _subView;

        public OptionsGroupPropertyDescriptor(string name, FrontedV3OptionsView subView)
            : base(name, null)
        {
            _subView = subView;
        }

        public override Type ComponentType => typeof(FrontedV3OptionsView);

        public override Type PropertyType => typeof(FrontedV3OptionsView);

        public override bool IsReadOnly => true;

        public override bool CanResetValue(object component) => false;

        public override void ResetValue(object component)
        {
        }

        public override object? GetValue(object? component)
        {
            return _subView;
        }

        public override void SetValue(object? component, object? value)
        {
            // 分组节点为只读代理，不支持赋值。
        }

        public override bool ShouldSerializeValue(object component) => false;
    }
}
