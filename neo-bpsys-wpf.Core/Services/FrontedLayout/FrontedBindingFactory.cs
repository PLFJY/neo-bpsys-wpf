using neo_bpsys_wpf.Core.Abstractions.Services;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Data;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 为 Designer v3 控件创建共享数据绑定。
/// </summary>
/// <remarks>
/// 不含动态索引的路径仍交给 WPF <see cref="PropertyPath"/>，以保持既有包的行为。
/// </remarks>
public static class FrontedBindingFactory
{
    /// <summary>
    /// 根据路径创建单向绑定。
    /// </summary>
    /// <param name="path">相对于 <paramref name="source"/> 的绑定路径。</param>
    /// <param name="source">共享数据根。</param>
    /// <returns>可直接传给 <see cref="BindingOperations.SetBinding(DependencyObject,DependencyProperty,BindingBase)"/> 的绑定。</returns>
    public static Binding Create(string path, ISharedDataService source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(source);

        if (!FrontedBindingPathParser.ContainsDynamicIndexer(path))
        {
            return new Binding(path) { Source = source, Mode = BindingMode.OneWay };
        }

        if (!FrontedBindingPathParser.TryParse(path, out var parsed, out _))
        {
            // Validator reports the syntax error. Runtime deliberately falls back to an unset value.
            return new Binding(nameof(FrontedDynamicBindingSource.Value))
            {
                Source = FrontedDynamicBindingSource.CreateInvalid(),
                Mode = BindingMode.OneWay
            };
        }

        return new Binding(nameof(FrontedDynamicBindingSource.Value))
        {
            Source = new FrontedDynamicBindingSource(source, parsed),
            Mode = BindingMode.OneWay
        };
    }
}

/// <summary>
/// Designer v3 的轻量绑定路径解析器。
/// </summary>
public static class FrontedBindingPathParser
{
    /// <summary>
    /// 判断路径是否包含动态索引语法。
    /// </summary>
    /// <param name="path">待检查的绑定路径。</param>
    /// <returns>包含非 literal 的索引项时为 <see langword="true"/>。</returns>
    public static bool ContainsDynamicIndexer(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var inQuote = false;
        var quote = '\0';
        for (var i = 0; i < path.Length; i++)
        {
            var character = path[i];
            if (inQuote)
            {
                if (character == '\\')
                {
                    i++;
                }
                else if (character == quote)
                {
                    inQuote = false;
                }
                continue;
            }

            if (character is '\'' or '\"')
            {
                inQuote = true;
                quote = character;
                continue;
            }

            if (character != '[')
            {
                continue;
            }

            var end = FindIndexerEnd(path, i + 1);
            if (end < 0)
            {
                return true;
            }

            var content = path[(i + 1)..end].Trim();
            if (!IsLiteral(content))
            {
                return true;
            }

            i = end;
        }

        return false;
    }

    /// <summary>
    /// 解析受支持的轻量绑定路径。
    /// </summary>
    /// <param name="text">绑定路径文本。</param>
    /// <param name="path">成功时返回的解析结果。</param>
    /// <param name="error">失败时返回的具体诊断。</param>
    /// <returns>路径有效时为 <see langword="true"/>。</returns>
    public static bool TryParse(string text, out FrontedBindingPath path, out FrontedBindingPathParseError? error)
    {
        path = new FrontedBindingPath([]);
        error = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            error = new FrontedBindingPathParseError(0, "BindingPath cannot be empty.");
            return false;
        }

        var segments = new List<FrontedBindingPathSegment>();
        var index = 0;
        var needsProperty = true;
        while (index < text.Length)
        {
            if (needsProperty)
            {
                if (!IsIdentifierStart(text[index]))
                {
                    error = new FrontedBindingPathParseError(index, "Expected a property name.");
                    return false;
                }

                var start = index++;
                while (index < text.Length && IsIdentifierPart(text[index]))
                {
                    index++;
                }
                segments.Add(new FrontedPropertyPathSegment(text[start..index], start));
                needsProperty = false;
                continue;
            }

            if (text[index] == '[')
            {
                var start = index;
                var end = FindIndexerEnd(text, ++index);
                if (end < 0)
                {
                    error = new FrontedBindingPathParseError(start, "Indexer is missing its closing ']'.");
                    return false;
                }

                var content = text[index..end].Trim();
                if (content.Length == 0)
                {
                    error = new FrontedBindingPathParseError(start, "Indexer cannot be empty.");
                    return false;
                }

                if (TryParseLiteral(content, out var literal))
                {
                    segments.Add(new FrontedLiteralIndexerPathSegment(literal, start));
                }
                else
                {
                    if (!TryParse(content, out var dynamicPath, out var dynamicError))
                    {
                        error = new FrontedBindingPathParseError(
                            start + 1 + (dynamicError?.Position ?? 0),
                            $"Dynamic index path is invalid: {dynamicError?.Message}");
                        return false;
                    }
                    segments.Add(new FrontedDynamicIndexerPathSegment(dynamicPath, content, start));
                }

                index = end + 1;
                continue;
            }

            if (text[index] == '.')
            {
                index++;
                needsProperty = true;
                continue;
            }

            error = new FrontedBindingPathParseError(index, "Expected '.' or an indexer.");
            return false;
        }

        if (needsProperty)
        {
            error = new FrontedBindingPathParseError(text.Length, "BindingPath cannot end with '.'.");
            return false;
        }

        path = new FrontedBindingPath(segments);
        return true;
    }

    private static int FindIndexerEnd(string text, int start)
    {
        var inQuote = false;
        var quote = '\0';
        for (var index = start; index < text.Length; index++)
        {
            var character = text[index];
            if (inQuote)
            {
                if (character == '\\')
                {
                    index++;
                }
                else if (character == quote)
                {
                    inQuote = false;
                }
                continue;
            }

            if (character is '\'' or '\"')
            {
                inQuote = true;
                quote = character;
            }
            else if (character == ']')
            {
                return index;
            }
        }
        return -1;
    }

    private static bool IsLiteral(string content) => TryParseLiteral(content, out _);

    private static bool TryParseLiteral(string content, out object literal)
    {
        if (int.TryParse(content, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            literal = number;
            return true;
        }

        if (content.Length >= 2 && content[0] is '\'' or '\"' && content[^1] == content[0])
        {
            literal = content[1..^1].Replace($"\\{content[0]}", content[0].ToString(), StringComparison.Ordinal);
            return true;
        }

        literal = null!;
        return false;
    }

    private static bool IsIdentifierStart(char value) => char.IsLetter(value) || value == '_';
    private static bool IsIdentifierPart(char value) => char.IsLetterOrDigit(value) || value == '_';
}

/// <summary>
/// 已解析的 Designer v3 绑定路径。
/// </summary>
/// <param name="segments">按访问顺序排列的路径段。</param>
public sealed class FrontedBindingPath(IReadOnlyList<FrontedBindingPathSegment> segments)
{
    /// <summary>按访问顺序排列的路径段。</summary>
    public IReadOnlyList<FrontedBindingPathSegment> Segments { get; } = segments;
}

/// <summary>
/// 绑定路径段的基类。
/// </summary>
/// <param name="position">该段在原始文本中的起始位置。</param>
public abstract class FrontedBindingPathSegment(int position)
{
    /// <summary>该段在原始文本中的起始位置。</summary>
    public int Position { get; } = position;
}

/// <summary>属性访问路径段。</summary>
/// <param name="name">属性名。</param><param name="position">起始位置。</param>
public sealed class FrontedPropertyPathSegment(string name, int position) : FrontedBindingPathSegment(position)
{
    /// <summary>属性名。</summary>
    public string Name { get; } = name;
}

/// <summary>literal 索引路径段。</summary>
/// <param name="value">literal 索引或键。</param><param name="position">起始位置。</param>
public sealed class FrontedLiteralIndexerPathSegment(object value, int position) : FrontedBindingPathSegment(position)
{
    /// <summary>literal 索引或键。</summary>
    public object Value { get; } = value;
}

/// <summary>从共享数据根重新解析的动态索引路径段。</summary>
/// <param name="path">动态索引路径。</param><param name="text">原始动态索引文本。</param><param name="position">起始位置。</param>
public sealed class FrontedDynamicIndexerPathSegment(FrontedBindingPath path, string text, int position) : FrontedBindingPathSegment(position)
{
    /// <summary>动态索引路径。</summary>
    public FrontedBindingPath Path { get; } = path;
    /// <summary>原始动态索引文本。</summary>
    public string Text { get; } = text;
}

/// <summary>绑定路径语法错误。</summary>
/// <param name="position">错误位置。</param><param name="message">错误说明。</param>
public sealed record FrontedBindingPathParseError(int Position, string Message);

/// <summary>
/// 为 v3 renderer 和插件 renderer 提供一致的无调用属性、索引读取规则。
/// </summary>
public static class FrontedBindingPathValueAccessor
{
    /// <summary>
    /// 尝试读取公开非索引属性。
    /// </summary>
    /// <param name="current">当前对象。</param>
    /// <param name="propertyName">属性名称。</param>
    /// <param name="value">成功时读取到的值。</param>
    /// <returns>属性可读取时为 <see langword="true"/>。</returns>
    public static bool TryReadProperty(object current, string propertyName, out object? value)
    {
        var property = current.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        if (property is not { CanRead: true } || property.GetIndexParameters().Length != 0)
        {
            value = null;
            return false;
        }

        try
        {
            value = property.GetValue(current);
            return true;
        }
        catch (TargetInvocationException)
        {
            value = null;
            return false;
        }
    }

    /// <summary>
    /// 尝试读取数组、列表、字典或公开 default indexer。
    /// </summary>
    /// <param name="current">当前可索引对象。</param>
    /// <param name="key">索引或键。</param>
    /// <param name="value">成功时读取到的值。</param>
    /// <returns>索引可用且读取成功时为 <see langword="true"/>。</returns>
    public static bool TryReadIndexer(object current, object? key, out object? value)
    {
        value = null;
        if (key is null || ReferenceEquals(key, DependencyProperty.UnsetValue))
        {
            return false;
        }

        if (current is Array array && TryConvertIndex(key, typeof(int), out var arrayIndex)
            && arrayIndex is int index && index >= 0 && index < array.Length)
        {
            value = array.GetValue(index);
            return true;
        }
        if (current is IList list && TryConvertIndex(key, typeof(int), out var listIndex)
            && listIndex is int listPosition && listPosition >= 0 && listPosition < list.Count)
        {
            value = list[listPosition];
            return true;
        }
        if (current is IDictionary dictionary && dictionary.Contains(key))
        {
            value = dictionary[key];
            return true;
        }

        var indexer = current.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(property => property.CanRead && property.GetIndexParameters() is [var parameter]
                                        && TryConvertIndex(key, parameter.ParameterType, out _));
        if (indexer is null || !TryConvertIndex(key, indexer.GetIndexParameters()[0].ParameterType, out var convertedKey))
        {
            return false;
        }

        try
        {
            value = indexer.GetValue(current, [convertedKey]);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentOutOfRangeException or KeyNotFoundException or TargetInvocationException)
        {
            return false;
        }
    }

    /// <summary>
    /// 尝试将动态索引值转换为 indexer 参数类型。
    /// </summary>
    /// <param name="value">原始动态索引值。</param>
    /// <param name="targetType">indexer 参数类型。</param>
    /// <param name="converted">成功时转换后的值。</param>
    /// <returns>转换安全且明确时为 <see langword="true"/>。</returns>
    public static bool TryConvertIndex(object value, Type targetType, out object? converted)
    {
        var coreType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (coreType.IsInstanceOfType(value))
        {
            converted = value;
            return true;
        }
        if (coreType.IsEnum)
        {
            if (value is string text && Enum.TryParse(coreType, text, ignoreCase: false, out var enumValue))
            {
                converted = enumValue;
                return true;
            }
            if (value is IConvertible)
            {
                try
                {
                    converted = Enum.ToObject(coreType, Convert.ChangeType(value, Enum.GetUnderlyingType(coreType), CultureInfo.InvariantCulture)!);
                    return true;
                }
                catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException) { }
            }
        }
        if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(coreType))
        {
            try
            {
                converted = Convert.ChangeType(value, coreType, CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException) { }
        }

        converted = null;
        return false;
    }
}

internal sealed class FrontedDynamicBindingSource : INotifyPropertyChanged
{
    private readonly object? _root;
    private readonly FrontedBindingPath? _path;
    private readonly List<INotifyPropertyChanged> _propertySources = [];
    private readonly List<INotifyCollectionChanged> _collectionSources = [];
    private object? _value = DependencyProperty.UnsetValue;
    private bool _isEvaluating;

    public FrontedDynamicBindingSource(ISharedDataService root, FrontedBindingPath path)
    {
        _root = root;
        _path = path;
        Reevaluate();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public object? Value => _value;

    public static FrontedDynamicBindingSource CreateInvalid() => new();

    private FrontedDynamicBindingSource()
    {
    }

    private void Reevaluate()
    {
        if (_isEvaluating || _root is null || _path is null)
        {
            return;
        }

        _isEvaluating = true;
        try
        {
            Detach();
            _value = Evaluate(_path, _root);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidCastException or TargetInvocationException)
        {
            _value = DependencyProperty.UnsetValue;
        }
        finally
        {
            _isEvaluating = false;
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
    }

    private object? Evaluate(FrontedBindingPath path, object? current)
    {
        foreach (var segment in path.Segments)
        {
            if (current is null || ReferenceEquals(current, DependencyProperty.UnsetValue))
            {
                return DependencyProperty.UnsetValue;
            }

            Subscribe(current);
            current = segment switch
            {
                FrontedPropertyPathSegment property => FrontedBindingPathValueAccessor.TryReadProperty(current, property.Name, out var propertyValue)
                    ? propertyValue : DependencyProperty.UnsetValue,
                FrontedLiteralIndexerPathSegment literal => FrontedBindingPathValueAccessor.TryReadIndexer(current, literal.Value, out var literalValue)
                    ? literalValue : DependencyProperty.UnsetValue,
                FrontedDynamicIndexerPathSegment dynamicIndexer => FrontedBindingPathValueAccessor.TryReadIndexer(current, Evaluate(dynamicIndexer.Path, _root), out var dynamicValue)
                    ? dynamicValue : DependencyProperty.UnsetValue,
                _ => DependencyProperty.UnsetValue
            };
        }
        return current ?? DependencyProperty.UnsetValue;
    }

    private void Subscribe(object source)
    {
        if (source is INotifyPropertyChanged propertySource && !_propertySources.Contains(propertySource))
        {
            PropertyChangedEventManager.AddHandler(propertySource, OnSourcePropertyChanged, string.Empty);
            _propertySources.Add(propertySource);
        }
        if (source is INotifyCollectionChanged collectionSource && !_collectionSources.Contains(collectionSource))
        {
            CollectionChangedEventManager.AddHandler(collectionSource, OnCollectionChanged);
            _collectionSources.Add(collectionSource);
        }
    }

    private void Detach()
    {
        foreach (var source in _propertySources)
        {
            PropertyChangedEventManager.RemoveHandler(source, OnSourcePropertyChanged, string.Empty);
        }
        foreach (var source in _collectionSources)
        {
            CollectionChangedEventManager.RemoveHandler(source, OnCollectionChanged);
        }
        _propertySources.Clear();
        _collectionSources.Clear();
    }

    private void OnSourcePropertyChanged(object? sender, PropertyChangedEventArgs args) => Reevaluate();
    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args) => Reevaluate();
}
