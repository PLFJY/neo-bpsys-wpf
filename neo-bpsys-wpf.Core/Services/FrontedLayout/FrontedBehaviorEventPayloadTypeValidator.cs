using System.Globalization;
using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 验证并规范化插件行为事件允许使用的稳定负载类型。
/// </summary>
public static class FrontedBehaviorEventPayloadTypeValidator
{
    private static readonly HashSet<Type> SupportedTypes =
    [
        typeof(string), typeof(bool), typeof(byte), typeof(short), typeof(int), typeof(long),
        typeof(float), typeof(double), typeof(decimal), typeof(Guid), typeof(DateTime), typeof(DateTimeOffset)
    ];

    /// <summary>
    /// 判断 CLR 类型是否可安全用于行为事件负载。
    /// </summary>
    /// <param name="valueType">待检查类型。</param>
    /// <returns>支持时为 <see langword="true"/>。</returns>
    public static bool IsSupported(Type valueType)
    {
        ArgumentNullException.ThrowIfNull(valueType);
        var effectiveType = Nullable.GetUnderlyingType(valueType) ?? valueType;
        return effectiveType.IsEnum || SupportedTypes.Contains(effectiveType);
    }

    /// <summary>
    /// 验证 CLR 类型是否受支持。
    /// </summary>
    /// <param name="valueType">待检查类型。</param>
    /// <exception cref="FrontedLayoutConfigException">类型不属于稳定负载协议时抛出。</exception>
    public static void EnsureSupported(Type valueType)
    {
        if (!IsSupported(valueType))
        {
            throw new FrontedLayoutConfigException(
                $"Behavior event payload type '{valueType.FullName}' is not supported. " +
                "Use string, bool, numeric primitives, decimal, Guid, enum, DateTime, DateTimeOffset, or nullable versions.");
        }
    }

    /// <summary>
    /// 将发布值验证并规范化为 EventBus、过滤器和 JSON 共同支持的值。
    /// </summary>
    /// <param name="value">发布值。</param>
    /// <param name="declaredType">注册 schema 声明的类型。</param>
    /// <param name="fieldPath">用于错误消息的字段路径。</param>
    /// <returns>规范化后的稳定值；枚举为名称，Guid 与日期为 invariant 字符串。</returns>
    /// <exception cref="InvalidOperationException">值为空但类型不可空，或值类型与 schema 不匹配时抛出。</exception>
    public static object? Normalize(object? value, Type declaredType, string fieldPath)
    {
        EnsureSupported(declaredType);
        var nullableType = Nullable.GetUnderlyingType(declaredType);
        var effectiveType = nullableType ?? declaredType;
        if (value is null)
        {
            if (nullableType is not null || !declaredType.IsValueType || declaredType == typeof(string))
            {
                return null;
            }

            throw new InvalidOperationException(
                $"Behavior event payload field '{fieldPath}' requires a non-null value of type '{GetTypeName(declaredType)}'.");
        }

        if (!effectiveType.IsInstanceOfType(value))
        {
            throw new InvalidOperationException(
                $"Behavior event payload field '{fieldPath}' expects '{GetTypeName(declaredType)}', " +
                $"but received '{value.GetType().FullName}'.");
        }

        if (effectiveType.IsEnum)
        {
            var name = Enum.GetName(effectiveType, value);
            if (name is null)
            {
                throw new InvalidOperationException(
                    $"Behavior event payload field '{fieldPath}' contains an undefined '{effectiveType.Name}' value.");
            }

            return name;
        }

        return value switch
        {
            Guid guid => guid.ToString("D", CultureInfo.InvariantCulture),
            DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
            _ => value
        };
    }

    /// <summary>
    /// 获取用于 Designer 元数据的稳定类型名。
    /// </summary>
    /// <param name="valueType">CLR 类型。</param>
    /// <returns>稳定类型名。</returns>
    public static string GetTypeName(Type valueType)
    {
        var nullable = Nullable.GetUnderlyingType(valueType);
        var effectiveType = nullable ?? valueType;
        var name = effectiveType == typeof(string) ? "string"
            : effectiveType == typeof(bool) ? "bool"
            : effectiveType == typeof(byte) ? "byte"
            : effectiveType == typeof(short) ? "short"
            : effectiveType == typeof(int) ? "int"
            : effectiveType == typeof(long) ? "long"
            : effectiveType == typeof(float) ? "float"
            : effectiveType == typeof(double) ? "double"
            : effectiveType == typeof(decimal) ? "decimal"
            : effectiveType.Name;
        return nullable is null ? name : $"{name}?";
    }
}
