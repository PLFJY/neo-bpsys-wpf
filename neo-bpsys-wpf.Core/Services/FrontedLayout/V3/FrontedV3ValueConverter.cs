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

        if (TryConvert(value, targetType, out var result))
        {
            return result;
        }

        var underlying = Nullable.GetUnderlyingType(targetType);
        var effectiveType = underlying ?? targetType;
        return GetDefaultValue(effectiveType);
    }

    /// <summary>
    /// 尝试将原始值转换为目标类型，不抛出异常。
    /// </summary>
    /// <param name="value">原始值，可能为 <see cref="JsonElement"/>、字符串、数字、enum 对象等。</param>
    /// <param name="targetType">目标类型，可为 nullable 或枚举。</param>
    /// <param name="result">转换成功时为转换后的值；失败时为 <see langword="null"/>。</param>
    /// <returns>转换成功时为 <see langword="true"/>；失败时为 <see langword="false"/>。</returns>
    /// <remarks>
    /// <para>
    /// 该方法统一处理 PropertyGrid 编辑提交、Storage 读写与 Options 代理的值转换，
    /// 避免调用方各自调用 <see cref="Convert(object?, Type)"/> 后再捕获异常判断失败。
    /// </para>
    /// <para>
    /// 支持以下转换路径：
    /// <list type="bullet">
    /// <item><see langword="null"/> → nullable 目标类型（返回 <see langword="true"/> 与 <see langword="null"/>）。</item>
    /// <item><see cref="JsonElement"/> → 任意目标类型（与 <see cref="Convert"/> 路径一致）。</item>
    /// <item>已是目标类型或可分配类型 → 原样返回。</item>
    /// <item>enum 目标类型：支持 string 名字解析与 int/long/底层值转换。</item>
    /// <item><see cref="Color"/>/<see cref="Brush"/> 字符串解析。</item>
    /// <item><see cref="string"/> 目标类型：调用 <see cref="object.ToString"/>。</item>
    /// <item>其他 <see cref="IConvertible"/> 类型：通过 <see cref="System.Convert.ChangeType(object, Type, IFormatProvider)"/> 转换。</item>
    /// </list>
    /// </para>
    /// </remarks>
    public static bool TryConvert(object? value, Type targetType, out object? result)
    {
        ArgumentNullException.ThrowIfNull(targetType);

        var underlying = Nullable.GetUnderlyingType(targetType);
        var effectiveType = underlying ?? targetType;

        if (value is null)
        {
            if (underlying is not null)
            {
                result = null;
                return true;
            }

            result = GetDefaultValue(effectiveType);
            return true;
        }

        if (value is JsonElement element)
        {
            return TryConvertJsonElement(element, effectiveType, underlying is not null, out result);
        }

        var valueType = value.GetType();
        if (effectiveType.IsAssignableFrom(valueType))
        {
            result = value;
            return true;
        }

        if (effectiveType.IsEnum)
        {
            return TryConvertToEnum(value, effectiveType, out result);
        }

        if (effectiveType == typeof(Color))
        {
            return TryConvertToColor(value, out result);
        }

        if (effectiveType == typeof(Brush))
        {
            return TryConvertToBrush(value, out result);
        }

        if (effectiveType == typeof(string))
        {
            result = value.ToString();
            return true;
        }

        try
        {
            result = System.Convert.ChangeType(value, effectiveType, CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception)
        {
            result = GetDefaultValue(effectiveType);
            return false;
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
        if (TryConvertJsonElement(element, effectiveType, isNullable, out var result))
        {
            return result;
        }

        return GetDefaultValue(effectiveType);
    }

    private static bool TryConvertJsonElement(
        JsonElement element,
        Type effectiveType,
        bool isNullable,
        out object? result)
    {
        if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
        {
            if (isNullable)
            {
                result = null;
                return true;
            }

            result = GetDefaultValue(effectiveType);
            return true;
        }

        if (effectiveType == typeof(string))
        {
            result = element.ValueKind == JsonValueKind.String
                ? element.GetString()
                : element.GetRawText();
            return true;
        }

        if (effectiveType == typeof(bool))
        {
            result = element.ValueKind == JsonValueKind.True;
            return true;
        }

        if (effectiveType == typeof(int))
        {
            if (element.TryGetInt32(out var i))
            {
                result = i;
                return true;
            }

            result = 0;
            return false;
        }

        if (effectiveType == typeof(long))
        {
            if (element.TryGetInt64(out var l))
            {
                result = l;
                return true;
            }

            result = 0L;
            return false;
        }

        if (effectiveType == typeof(double))
        {
            if (element.TryGetDouble(out var d))
            {
                result = d;
                return true;
            }

            result = 0d;
            return false;
        }

        if (effectiveType == typeof(float))
        {
            if (element.TryGetSingle(out var f))
            {
                result = f;
                return true;
            }

            result = 0f;
            return false;
        }

        if (effectiveType.IsEnum)
        {
            var raw = element.ValueKind == JsonValueKind.String
                ? element.GetString()
                : element.GetRawText();
            return TryConvertToEnum(raw ?? string.Empty, effectiveType, out result);
        }

        if (effectiveType == typeof(Color))
        {
            var text = element.ValueKind == JsonValueKind.String ? element.GetString() : element.GetRawText();
            return TryConvertToColor(text ?? string.Empty, out result);
        }

        if (effectiveType == typeof(Brush))
        {
            var text = element.ValueKind == JsonValueKind.String ? element.GetString() : element.GetRawText();
            return TryConvertToBrush(text ?? string.Empty, out result);
        }

        try
        {
            result = JsonSerializer.Deserialize(element, effectiveType);
            return result is not null;
        }
        catch (Exception)
        {
            result = GetDefaultValue(effectiveType);
            return false;
        }
    }

    private static object ConvertToEnum(object value, Type enumType)
    {
        if (TryConvertToEnum(value, enumType, out var result))
        {
            return result;
        }

        return Activator.CreateInstance(enumType)!;
    }

    private static bool TryConvertToEnum(object value, Type enumType, out object? result)
    {
        if (value is string s)
        {
            if (Enum.TryParse(enumType, s, out var parsed))
            {
                result = parsed;
                return true;
            }

            result = Activator.CreateInstance(enumType);
            return false;
        }

        if (value is int i)
        {
            result = Enum.ToObject(enumType, i);
            return true;
        }

        if (value is long l)
        {
            result = Enum.ToObject(enumType, l);
            return true;
        }

        try
        {
            result = Enum.ToObject(enumType, System.Convert.ToInt64(value, CultureInfo.InvariantCulture));
            return true;
        }
        catch (Exception)
        {
            result = Activator.CreateInstance(enumType);
            return false;
        }
    }

    private static object ConvertToColor(object value)
    {
        if (TryConvertToColor(value, out var result))
        {
            return result;
        }

        return Colors.White;
    }

    private static bool TryConvertToColor(object value, out object? result)
    {
        if (value is Color c)
        {
            result = c;
            return true;
        }

        var text = value as string ?? value.ToString();
        if (ColorHelper.TryParseColor(text, out var color))
        {
            result = color;
            return true;
        }

        result = Colors.White;
        return false;
    }

    private static object ConvertToBrush(object value)
    {
        if (TryConvertToBrush(value, out var result))
        {
            return result;
        }

        return ColorHelper.CreateBrushOrDefault(string.Empty, Colors.White);
    }

    private static bool TryConvertToBrush(object value, out object? result)
    {
        if (value is Brush brush)
        {
            result = brush;
            return true;
        }

        var text = value as string ?? value.ToString();
        if (ColorHelper.TryParseColor(text, out var color))
        {
            result = ColorHelper.CreateBrushOrDefault(text, color);
            return true;
        }

        result = ColorHelper.CreateBrushOrDefault(text, Colors.White);
        return false;
    }

    private static object? GetDefaultValue(Type type)
    {
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}
