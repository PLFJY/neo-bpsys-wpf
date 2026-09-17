using System.Globalization;
using neo_bpsys_wpf.Converters;
using Xunit;

namespace neo_bpsys_wpf.Tests.Converters;

/// <summary>
/// 测试队员删除确认文本保持迁移前的拼接格式。
/// </summary>
public sealed class MemberDeleteConfirmationTextConverterTest
{
    [Theory]
    [InlineData("是否删除", "Alice", "是否删除  \"Alice\" ？")]
    [InlineData("是否删除", "", "是否删除 ？")]
    [InlineData("Are you sure to delete", null, "Are you sure to delete ？")]
    public void Convert_ShouldPreserveOriginalConcatenation(
        string prompt,
        string memberName,
        string expected)
    {
        var converter = new MemberDeleteConfirmationTextConverter();

        var result = converter.Convert(
            [prompt, memberName],
            typeof(string),
            parameter: null,
            CultureInfo.InvariantCulture);

        Assert.Equal(expected, result);
    }
}
