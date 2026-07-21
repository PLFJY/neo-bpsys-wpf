using neo_bpsys_wpf.Core.Services.Archives;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public sealed class ArchiveProgressParserTest
{
    [Fact]
    public void Parse_SinglePercentage_ReportsOnce()
    {
        var lastReported = -1;
        var (reported, remaining) = SevenZipProgressParser.Parse("50%", ref lastReported);

        Assert.Single(reported);
        Assert.Equal(50, reported[0]);
        Assert.Equal(50, lastReported);
        Assert.Equal(string.Empty, remaining);
    }

    [Fact]
    public void Parse_MultiplePercentagesInOneChunk_ReportsAll()
    {
        var lastReported = -1;
        var (reported, remaining) = SevenZipProgressParser.Parse("10% 20% 30%", ref lastReported);

        Assert.Equal(new[] { 10, 20, 30 }, reported);
        Assert.Equal(30, lastReported);
        Assert.Equal(string.Empty, remaining);
    }

    [Fact]
    public void Parse_CarriageReturnSeparated_ReportsAll()
    {
        var lastReported = -1;
        var (reported, remaining) = SevenZipProgressParser.Parse("10%\r20%\r30%", ref lastReported);

        Assert.Equal(new[] { 10, 20, 30 }, reported);
        Assert.Equal(30, lastReported);
        Assert.Equal(string.Empty, remaining);
    }

    [Fact]
    public void Parse_PercentageSplitAcrossChunks_ReportsWhenComplete()
    {
        var lastReported = -1;

        // First chunk: "5" — no % yet, should buffer the trailing digit
        var (reported1, remaining1) = SevenZipProgressParser.Parse("5", ref lastReported);
        Assert.Empty(reported1);
        Assert.Equal("5", remaining1);
        Assert.Equal(-1, lastReported);

        // Second chunk: prepend remaining buffer, "50%" should match
        var (reported2, remaining2) = SevenZipProgressParser.Parse(remaining1 + "0%", ref lastReported);
        Assert.Single(reported2);
        Assert.Equal(50, reported2[0]);
        Assert.Equal(50, lastReported);
        Assert.Equal(string.Empty, remaining2);
    }

    [Fact]
    public void Parse_DuplicatePercentage_ReportsOnce()
    {
        var lastReported = -1;
        var (reported, remaining) = SevenZipProgressParser.Parse("50% 50%", ref lastReported);

        Assert.Single(reported);
        Assert.Equal(50, reported[0]);
        Assert.Equal(50, lastReported);
        Assert.Equal(string.Empty, remaining);
    }

    [Fact]
    public void Parse_RegressingPercentage_NotReported()
    {
        var lastReported = -1;
        var (reported, remaining) = SevenZipProgressParser.Parse("50% 30%", ref lastReported);

        Assert.Single(reported);
        Assert.Equal(50, reported[0]);
        Assert.Equal(50, lastReported);
        Assert.Equal(string.Empty, remaining);
    }

    [Fact]
    public void Parse_NonPercentageOutput_NotReported()
    {
        var lastReported = -1;
        var (reported, remaining) = SevenZipProgressParser.Parse("7-Zip [64] 23.01", ref lastReported);

        Assert.Empty(reported);
        Assert.Equal(-1, lastReported);
        Assert.Equal("01", remaining);
    }

    [Fact]
    public void Parse_Over100Percentage_NotReported()
    {
        var lastReported = -1;
        var (reported, remaining) = SevenZipProgressParser.Parse("150%", ref lastReported);

        // "150%" — regex matches "150" but 150 > 100 so it's filtered out.
        // However, "50%" within "150%" is not matched because of the (?<!\d) lookbehind.
        // So nothing is reported.
        Assert.Empty(reported);
        Assert.Equal(-1, lastReported);
        Assert.Equal(string.Empty, remaining);
    }

    [Fact]
    public void Parse_EmptyInput_NoReport()
    {
        var lastReported = -1;
        var (reported, remaining) = SevenZipProgressParser.Parse("", ref lastReported);

        Assert.Empty(reported);
        Assert.Equal(-1, lastReported);
        Assert.Equal(string.Empty, remaining);
    }

    [Fact]
    public void Parse_BoundaryValues_ReportsCorrectly()
    {
        var lastReported = -1;
        var (reported, remaining) = SevenZipProgressParser.Parse("0% 99% 100%", ref lastReported);

        Assert.Equal(new[] { 0, 99, 100 }, reported);
        Assert.Equal(100, lastReported);
        Assert.Equal(string.Empty, remaining);
    }

    [Fact]
    public void Parse_ZeroPercentage_AfterNonZero_NotReported()
    {
        var lastReported = -1;
        // First report 50
        SevenZipProgressParser.Parse("50%", ref lastReported);
        Assert.Equal(50, lastReported);

        // Then 0% — should not regress
        var (reported, remaining) = SevenZipProgressParser.Parse("0%", ref lastReported);
        Assert.Empty(reported);
        Assert.Equal(50, lastReported);
        Assert.Equal(string.Empty, remaining);
    }
}
