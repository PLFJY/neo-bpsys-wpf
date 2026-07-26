using System;
using System.Text.Json;
using System.Windows.Media;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// 测试 <see cref="FrontedV3ValueConverter"/> 的 <see cref="FrontedV3ValueConverter.TryConvert"/>
/// 与 <see cref="FrontedV3ValueConverter.Convert"/> 路径，覆盖 enum、nullable enum、JsonElement、
/// Color/Brush、自定义类型与 <see cref="IConvertible"/> 兜底。
/// </summary>
/// <remarks>
/// <para>
/// 这些测试验证 Designer V3 验收 Round-3 P1-1 的契约：
/// PropertyGrid 提交 enum/自定义类型时，<c>ApplySchemaPropertyEdit</c> 通过
/// <see cref="FrontedV3ValueConverter.TryConvert"/> 完成转换，不再走
/// <see cref="System.Convert.ChangeType(object, Type)"/> 路径导致 enum 解析失败。
/// </para>
/// <para>
/// 这些是纯逻辑测试，不涉及 WPF 视觉树，因此不需要
/// <see cref="neo_bpsys_wpf.Tests.Infrastructure.WpfTestThread"/>。
/// </para>
/// </remarks>
public class FrontedV3ValueConverterTest
{
    // -------------------------------------------------------------------
    // 1. Enum from string
    // -------------------------------------------------------------------

    /// <summary>
    /// <see cref="FrontedV3ValueConverter.TryConvert"/> 必须能把 enum 名字字符串解析为目标 enum 类型。
    /// </summary>
    [Fact]
    public void TryConvert_ParsesEnumFromString()
    {
        var ok = FrontedV3ValueConverter.TryConvert(
            nameof(DayOfWeek.Wednesday),
            typeof(DayOfWeek),
            out var result);

        Assert.True(ok);
        Assert.Equal(DayOfWeek.Wednesday, result);
    }

    // -------------------------------------------------------------------
    // 2. Enum from int
    // -------------------------------------------------------------------

    /// <summary>
    /// <see cref="FrontedV3ValueConverter.TryConvert"/> 必须能把整数值转换为 enum。
    /// </summary>
    [Fact]
    public void TryConvert_ConvertsEnumFromInt()
    {
        var ok = FrontedV3ValueConverter.TryConvert(3, typeof(DayOfWeek), out var result);

        Assert.True(ok);
        Assert.Equal(DayOfWeek.Wednesday, result);
    }

    // -------------------------------------------------------------------
    // 3. Enum from existing enum instance (assignable fast path)
    // -------------------------------------------------------------------

    /// <summary>
    /// 当 value 已是目标 enum 类型时，<see cref="FrontedV3ValueConverter.TryConvert"/> 必须走快速路径原样返回。
    /// </summary>
    [Fact]
    public void TryConvert_AcceptsExistingEnumInstance()
    {
        var ok = FrontedV3ValueConverter.TryConvert(
            DayOfWeek.Friday,
            typeof(DayOfWeek),
            out var result);

        Assert.True(ok);
        Assert.Equal(DayOfWeek.Friday, result);
    }

    // -------------------------------------------------------------------
    // 4. Nullable enum
    // -------------------------------------------------------------------

    /// <summary>
    /// <see cref="FrontedV3ValueConverter.TryConvert"/> 必须能把字符串解析为 nullable enum 类型。
    /// </summary>
    [Fact]
    public void TryConvert_ParsesNullableEnumFromString()
    {
        var ok = FrontedV3ValueConverter.TryConvert(
            nameof(DayOfWeek.Monday),
            typeof(DayOfWeek?),
            out var result);

        Assert.True(ok);
        Assert.Equal(DayOfWeek.Monday, result);
    }

    /// <summary>
    /// 当 value 为 <see langword="null"/> 且目标为 nullable enum 时，
    /// <see cref="FrontedV3ValueConverter.TryConvert"/> 必须返回 <see langword="true"/> 与 <see langword="null"/>。
    /// </summary>
    [Fact]
    public void TryConvert_NullValueForNullableEnumReturnsNull()
    {
        var ok = FrontedV3ValueConverter.TryConvert(null, typeof(DayOfWeek?), out var result);

        Assert.True(ok);
        Assert.Null(result);
    }

    // -------------------------------------------------------------------
    // 5. Enum from invalid string returns false
    // -------------------------------------------------------------------

    /// <summary>
    /// 当字符串无法解析为 enum 时，<see cref="FrontedV3ValueConverter.TryConvert"/> 必须返回 <see langword="false"/>，
    /// 而不是抛出异常或返回默认值假装成功。
    /// </summary>
    [Fact]
    public void TryConvert_InvalidEnumStringReturnsFalse()
    {
        var ok = FrontedV3ValueConverter.TryConvert(
            "NotADay",
            typeof(DayOfWeek),
            out var result);

        Assert.False(ok);
    }

    // -------------------------------------------------------------------
    // 6. JsonElement enum round-trip
    // -------------------------------------------------------------------

    /// <summary>
    /// <see cref="FrontedV3ValueConverter.TryConvert"/> 必须能把 <see cref="JsonElement"/>（字符串形式）解析为 enum。
    /// 这覆盖了插件属性从 <c>ExtensionData</c> 读出后还原为强类型 enum 的场景。
    /// </summary>
    [Fact]
    public void TryConvert_ParsesEnumFromJsonElementString()
    {
        var element = JsonSerializer.SerializeToElement(nameof(DayOfWeek.Saturday));

        var ok = FrontedV3ValueConverter.TryConvert(element, typeof(DayOfWeek), out var result);

        Assert.True(ok);
        Assert.Equal(DayOfWeek.Saturday, result);
    }

    /// <summary>
    /// <see cref="FrontedV3ValueConverter.TryConvert"/> 必须能把 <see cref="JsonElement"/>（数字形式）解析为 enum。
    /// </summary>
    [Fact]
    public void TryConvert_ParsesEnumFromJsonElementNumber()
    {
        var element = JsonSerializer.SerializeToElement(6);

        var ok = FrontedV3ValueConverter.TryConvert(element, typeof(DayOfWeek), out var result);

        Assert.True(ok);
        Assert.Equal(DayOfWeek.Saturday, result);
    }

    // -------------------------------------------------------------------
    // 7. JsonElement string → string
    // -------------------------------------------------------------------

    /// <summary>
    /// <see cref="FrontedV3ValueConverter.TryConvert"/> 必须能把 <see cref="JsonElement"/>（字符串形式）还原为 string。
    /// </summary>
    [Fact]
    public void TryConvert_ParsesStringFromJsonElement()
    {
        var element = JsonSerializer.SerializeToElement("ASG");

        var ok = FrontedV3ValueConverter.TryConvert(element, typeof(string), out var result);

        Assert.True(ok);
        Assert.Equal("ASG", result);
    }

    // -------------------------------------------------------------------
    // 8. JsonElement number → double
    // -------------------------------------------------------------------

    /// <summary>
    /// <see cref="FrontedV3ValueConverter.TryConvert"/> 必须能把 <see cref="JsonElement"/>（数字形式）还原为 double。
    /// </summary>
    [Fact]
    public void TryConvert_ParsesDoubleFromJsonElement()
    {
        var element = JsonSerializer.SerializeToElement(12.5);

        var ok = FrontedV3ValueConverter.TryConvert(element, typeof(double), out var result);

        Assert.True(ok);
        Assert.Equal(12.5d, result);
    }

    // -------------------------------------------------------------------
    // 9. JsonElement null → nullable<T>
    // -------------------------------------------------------------------

    /// <summary>
    /// 当 <see cref="JsonElement"/> 为 null 且目标为 nullable 值类型时，
    /// <see cref="FrontedV3ValueConverter.TryConvert"/> 必须返回 <see langword="true"/> 与 <see langword="null"/>。
    /// </summary>
    [Fact]
    public void TryConvert_NullJsonElementReturnsNullForNullable()
    {
        var element = JsonSerializer.SerializeToElement((object?)null);

        var ok = FrontedV3ValueConverter.TryConvert(element, typeof(int?), out var result);

        Assert.True(ok);
        Assert.Null(result);
    }

    // -------------------------------------------------------------------
    // 10. Custom non-IConvertible type passes through when assignable
    // -------------------------------------------------------------------

    /// <summary>
    /// 当 value 已是目标类型或可分配类型时，<see cref="FrontedV3ValueConverter.TryConvert"/> 必须原样返回。
    /// 这覆盖了 PropertyGrid 下拉框直接提供 <c>FrontedTextBindingExpression</c> 等自定义对象的场景。
    /// </summary>
    [Fact]
    public void TryConvert_AcceptsCustomTypeWhenAssignable()
    {
        var instance = new CustomNonConvertibleType();

        var ok = FrontedV3ValueConverter.TryConvert(instance, typeof(CustomNonConvertibleType), out var result);

        Assert.True(ok);
        Assert.Same(instance, result);
    }

    // -------------------------------------------------------------------
    // 11. Custom non-IConvertible type conversion fails gracefully
    // -------------------------------------------------------------------

    /// <summary>
    /// 当 value 既不是目标类型、也不是 <see cref="IConvertible"/>、又不是 enum/Color/Brush 时，
    /// <see cref="FrontedV3ValueConverter.TryConvert"/> 必须返回 <see langword="false"/> 而不抛出异常。
    /// </summary>
    [Fact]
    public void TryConvert_FailsGracefullyForNonConvertibleMismatch()
    {
        var value = new CustomNonConvertibleType();

        var ok = FrontedV3ValueConverter.TryConvert(value, typeof(AnotherCustomType), out _);

        Assert.False(ok);
    }

    // -------------------------------------------------------------------
    // 12. Color string parsing
    // -------------------------------------------------------------------

    /// <summary>
    /// <see cref="FrontedV3ValueConverter.TryConvert"/> 必须能把 ARGB 十六进制字符串解析为 <see cref="Color"/>。
    /// </summary>
    [Fact]
    public void TryConvert_ParsesColorFromArgbString()
    {
        var ok = FrontedV3ValueConverter.TryConvert("#FFAA0011", typeof(Color), out var result);

        Assert.True(ok);
        var color = Assert.IsType<Color>(result);
        Assert.Equal(0xFF, color.A);
        Assert.Equal(0xAA, color.R);
        Assert.Equal(0x00, color.G);
        Assert.Equal(0x11, color.B);
    }

    // -------------------------------------------------------------------
    // 13. Convert preserves old behavior (returns default on failure)
    // -------------------------------------------------------------------

    /// <summary>
    /// <see cref="FrontedV3ValueConverter.Convert"/> 在失败时仍必须返回目标类型默认值，保持向后兼容。
    /// </summary>
    [Fact]
    public void Convert_ReturnsDefaultOnFailure()
    {
        var result = FrontedV3ValueConverter.Convert("NotADay", typeof(DayOfWeek));

        Assert.Equal(DayOfWeek.Sunday, result);
    }

    // -------------------------------------------------------------------
    // 14. Convert handles nullable null
    // -------------------------------------------------------------------

    /// <summary>
    /// <see cref="FrontedV3ValueConverter.Convert"/> 在 value 为 <see langword="null"/> 且目标为 nullable 时必须返回 <see langword="null"/>。
    /// </summary>
    [Fact]
    public void Convert_ReturnsNullForNullableNullValue()
    {
        var result = FrontedV3ValueConverter.Convert(null, typeof(int?));

        Assert.Null(result);
    }

    private sealed class CustomNonConvertibleType;

    private sealed class AnotherCustomType;
}
