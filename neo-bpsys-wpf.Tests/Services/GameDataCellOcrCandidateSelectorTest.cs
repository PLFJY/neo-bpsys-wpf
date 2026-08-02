extern alias smartbp;

using System.Collections.Generic;
using Xunit;
using GameDataCellOcrCandidate = smartbp::neo_bpsys_wpf.Services.GameDataCellOcrCandidate;
using GameDataCellOcrCandidateSelector = smartbp::neo_bpsys_wpf.Services.GameDataCellOcrCandidateSelector;

namespace neo_bpsys_wpf.Tests.Services;

public sealed class GameDataCellOcrCandidateSelectorTest
{
    [Theory]
    [InlineData("１", "1")]
    [InlineData("I", "1")]
    [InlineData("丨", "1")]
    [InlineData("205%", "205")]
    public void NormalizeNumericText_NormalizesSupportedNumericForms(string raw, string expected)
    {
        Assert.Equal(expected, GameDataCellOcrCandidateSelector.NormalizeNumericText(raw));
    }

    [Fact]
    public void Select_AcceptsTwoVariantsAgreeingOnOne()
    {
        var result = GameDataCellOcrCandidateSelector.Select(
        [
            Candidate("original", "1", 0.72),
            Candidate("contrast", "I", 0.76),
            Candidate("threshold", "丨", 0.70)
        ]);

        Assert.NotNull(result);
        Assert.Equal("1", result.Value);
        Assert.Equal(3, result.SupportCount);
    }

    [Fact]
    public void Select_AcceptsNoisyTrailingCharacterOnlyWhenItSupportsExactDigit()
    {
        var result = GameDataCellOcrCandidateSelector.Select(
        [
            Candidate("original", "1", 0.624),
            Candidate("contrast", "1r", 0.431),
            Candidate("threshold", "", 0)
        ]);

        Assert.NotNull(result);
        Assert.Equal("1", result.Value);
        Assert.Equal(2, result.SupportCount);
        Assert.Equal(0.624, result.Confidence, 3);
    }

    [Fact]
    public void Select_AcceptsLowConfidenceExactOneWhenVisualEvidenceConfirmsIt()
    {
        var result = GameDataCellOcrCandidateSelector.Select(
        [
            Candidate("threshold", "1", 0.771),
            new GameDataCellOcrCandidate("visual-vertical-stroke", "1", 0.70, "OpenCV/shape")
        ]);

        Assert.NotNull(result);
        Assert.Equal("1", result.Value);
        Assert.Equal(2, result.SupportCount);
        Assert.Equal(0.771, result.Confidence, 3);
    }

    [Fact]
    public void Select_RejectsNoisyTrailingCharacterWithoutExactDigit()
    {
        var result = GameDataCellOcrCandidateSelector.Select(
        [
            Candidate("original", "1r", 0.99),
            Candidate("contrast", "背景", 0.99)
        ]);

        Assert.Null(result);
    }

    [Fact]
    public void Select_RejectsSingleLowConfidenceDigitAndNonnumericHallucinations()
    {
        var result = GameDataCellOcrCandidateSelector.Select(
        [
            Candidate("original", "1", 0.62),
            Candidate("contrast", "背景", 0.99),
            Candidate("threshold", "-", 0.98)
        ]);

        Assert.Null(result);
    }

    [Fact]
    public void Select_AcceptsSingleHighConfidenceDigit()
    {
        var result = GameDataCellOcrCandidateSelector.Select(
        [
            Candidate("original", "14", 0.94),
            Candidate("contrast", "I4?", 0.81)
        ]);

        Assert.NotNull(result);
        Assert.Equal("14", result.Value);
        Assert.Equal(1, result.SupportCount);
    }

    private static GameDataCellOcrCandidate Candidate(string variant, string text, double confidence) =>
        new(variant, text, confidence, "test");
}
