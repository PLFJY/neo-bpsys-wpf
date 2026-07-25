using System.Globalization;
using System.Text.Json;
using System.Windows.Media;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout.V3;

/// <summary>
/// v3 前台控件属性值的类型转换器，统一处理存储原始值到目标 <c>PropertyType</c> 的转换。
/// </summary>
/// <remarks>
/// <para>
/// 支持以下转换路径：string、bool、int/double、enum、nullable、Color 字符串、<see cref="JsonElement"/> 到目标类型。
/// </para>
/// <para>
/// 该转换器是无状态静态工具，由 <see cref="Models.FrontedLayout.V3.Properties.FrontedV3PropertyDefinition"/>
/// 在读写 Config 时调用，确保 <see cref="PluginFrontedControlConfig.ExtensionData"/> 中的
/// <see cref="JsonElement"/> 能正确还原为强类型值。
/// </para>
/// </remarks>
public static class FrontedV3ValueConverter
{
    /// <summary>
    /// 将原始值转换为目标类型。
    /// </summary>
    /// <param name="value">原始值，可能为 <see cref="JsonElement"/>、字符串、数字等。</param>
    /// <param name="targetType">目标类型，可为 nullable 或枚举。</param>
    /// <returns>转换后的值；当 <paramref name="value"/> 为 <see langword="null"/> 且目标为值类型时返回类型默认值。</returns>
    public static object? Convert(object? value, Type targetType)
    {
        ArgumentNullException.ThrowIfNull(targetType);

        var underlying = Nullable.GetUnderlyingType(targetType);
        var effectiveType = underlying ?? targetType;

        if (value is null)
        {
            return underlying is not null ? null : GetDefaultValue(effectiveType);
        }

        if (value is JsonElement element)
        {
            return ConvertJsonElement(element, effectiveType, underlying is not null);
        }

        var valueType = value.GetType();
        if (effectiveType.IsAssignableFrom(valueType))
        {
            return value;
        }

        if (effectiveType.IsEnum)
        {
            return ConvertToEnum(value, effectiveType);
        }

        if (effectiveType == typeof(Color))
        {
            return ConvertToColor(value);
        }

        if (effectiveType == typeof(Brush))
        {
            return ConvertToBrush(value);
        }

        if (effectiveType == typeof(string))
        {
            return value.ToString();
        }

        try
        {
            return System.Convert.ChangeType(value, effectiveType, CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return GetDefaultValue(effectiveType);
        }
    }

    /// <summary>
    /// 将值序列化为 <see cref="JsonElement"/>，用于写回 <see cref="PluginFrontedControlConfig.ExtensionData"/>。
    /// </summary>
    /// <param name="value">要序列化的值。</param>
    /// <returns>表示该值的 <see cref="JsonElement"/>；<paramref name="value"/> 为 <see langword="null"/> 时返回 null 类型的 <see cref="JsonElement"/>。</returns>
    public static JsonElement ToJsonElement(object? value)
    {
        return JsonSerializer.SerializeToElement(value);
    }

    private static object? ConvertJsonElement(JsonElement element, Type effectiveType, bool isNullable)
    {
        if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
        {
            return isNullable ? null : GetDefaultValue(effectiveType);
        }

        if (effectiveType == typeof(string))
        {
            return element.ValueKind == JsonValueKind.String
                ? element.GetString()
                : element.GetRawText();
        }

        if (effectiveType == typeof(bool))
        {
            return element.ValueKind == JsonValueKind.True;
        }

        if (effectiveType == typeof(int))
        {
            return element.TryGetInt32(out var i) ? i : 0;
        }

        if (effectiveType == typeof(long))
        {
            return element.TryGetInt64(out var l) ? l : 0L;
        }

        if (effectiveType == typeof(double))
        {
            return element.TryGetDouble(out var d) ? d : 0d;
        }

        if (effectiveType == typeof(float))
        {
            return element.TryGetSingle(out var f) ? f : 0f;
        }

        if (effectiveType.IsEnum)
        {
            var raw = element.ValueKind == JsonValueKind.String
                ? element.GetString()
                : element.GetRawText();
            return ConvertToEnum(raw ?? string.Empty, effectiveType);
        }

        if (effectiveType == typeof(Color))
        {
            var text = element.ValueKind == JsonValueKind.String ? element.GetString() : element.GetRawText();
            return ConvertToColor(text ?? string.Empty);
        }

        if (effectiveType == typeof(Brush))
        {
            var text = element.ValueKind == JsonValueKind.String ? element.GetString() : element.GetRawText();
            return ConvertToBrush(text ?? string.Empty);
        }

        try
        {
            return JsonSerializer.Deserialize(element, effectiveType);
        }
        catch (Exception)
        {
            return GetDefaultValue(effectiveType);
        }
    }

    private static object ConvertToEnum(object value, Type enumType)
    {
        if (value is string s)
        {
            return Enum.TryParse(enumType, s, out var parsed)
                ? parsed
                : Activator.CreateInstance(enumType)!;
        }

        if (value is int i)
        {
            return Enum.ToObject(enumType, i);
        }

        if (value is long l)
        {
            return Enum.ToObject(enumType, l);
        }

        try
        {
            return Enum.ToObject(enumType, System.Convert.ToInt64(value, CultureInfo.InvariantCulture));
        }
        catch (Exception)
        {
            return Activator.CreateInstance(enumType)!;
        }
    }

    private static object ConvertToColor(object value)
    {
        if (value is Color c)
        {
            return c;
        }

        var text = value as string ?? value.ToString();
        return ColorHelper.TryParseColor(text, out var color) ? color : Colors.White;
    }

    private static object ConvertToBrush(object value)
    {
        if (value is Brush brush)
        {
            return brush;
        }

        var text = value as string ?? value.ToString();
        return ColorHelper.CreateBrushOrDefault(text, Colors.White);
    }

    private static object? GetDefaultValue(Type type)
    {
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}
