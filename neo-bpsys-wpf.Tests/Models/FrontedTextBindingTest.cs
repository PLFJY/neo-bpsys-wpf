using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Core.Abstractions.Services;
using System.Collections.ObjectModel;
using System.Globalization;
using System;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using Xunit;

namespace neo_bpsys_wpf.Tests.Models;

public class FrontedTextBindingTest
{
    [Fact]
    public void TextBindingModelsRoundTrip()
    {
        var config = new TextFrontedControlConfig
        {
            Text = "Static",
            TextBinding = new FrontedTextBindingExpression
            {
                Sources =
                [
                    new FrontedBindingSourceConfig { Path = "HomeTeam.Name", DisplayName = "Home" },
                    new FrontedBindingSourceConfig { Path = "AwayTeam.Name", Format = "ignored" }
                ],
                StringFormat = "{0} vs {1}",
                JoinSeparator = " - ",
                NullText = "N/A",
                FallbackText = "Fallback"
            }
        };

        var roundTrip = JsonSerializer.Deserialize<TextFrontedControlConfig>(JsonSerializer.Serialize(config));

        Assert.NotNull(roundTrip?.TextBinding);
        Assert.Equal(2, roundTrip.TextBinding.Sources.Count);
        Assert.Equal("HomeTeam.Name", roundTrip.TextBinding.Sources[0].Path);
        Assert.Equal("{0} vs {1}", roundTrip.TextBinding.StringFormat);
        Assert.Equal(" - ", roundTrip.TextBinding.JoinSeparator);
    }

    [Theory]
    [InlineData("{0}", "Home")]
    [InlineData("{0} : {1}", "Home : Away")]
    public void ConverterAppliesCompositeFormat(string format, string expected)
    {
        var values = format.Contains("{1}", StringComparison.Ordinal)
            ? new object[] { "Home", "Away" }
            : new object[] { "Home" };
        var expression = new FrontedTextBindingExpression { StringFormat = format };

        var result = Convert(values, expression);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ConverterJoinsValuesWhenStringFormatIsEmpty()
    {
        var result = Convert(
            ["Home", "Away"],
            new FrontedTextBindingExpression { JoinSeparator = " - " });

        Assert.Equal("Home - Away", result);
    }

    [Fact]
    public void ConverterHandlesNullUnavailableAndMalformedFormatWithoutThrowing()
    {
        Assert.Equal(
            "N/A",
            Convert([null!], new FrontedTextBindingExpression { NullText = "N/A" }));
        Assert.Equal(
            "Fallback",
            Convert([DependencyProperty.UnsetValue], new FrontedTextBindingExpression { FallbackText = "Fallback" }));
        Assert.Equal(
            "Fallback",
            Convert(["Home"], new FrontedTextBindingExpression { StringFormat = "{2}", FallbackText = "Fallback" }));
    }

    [Fact]
    public void ValidatorRejectsOutOfRangeStringFormatAndEmptySourcePath()
    {
        var item = new FrontedControlDesignItem
        {
            Name = "Title",
            Config = new TextFrontedControlConfig
            {
                TextBinding = new FrontedTextBindingExpression
                {
                    Sources =
                    [
                        new FrontedBindingSourceConfig { Path = "HomeTeam.Name" },
                        new FrontedBindingSourceConfig()
                    ],
                    StringFormat = "{2}"
                }
            }
        };
        var document = new FrontedCanvasDesignDocument
        {
            WindowTypeName = "TestWindow",
            CanvasName = "BaseCanvas",
            CanvasConfig = new FrontedCanvasConfig(),
            Controls = new ObservableCollection<FrontedControlDesignItem> { item }
        };

        var messages = new FrontedLayoutValidator().Validate(document);

        Assert.Contains(messages, message => message.Code == "TextBindingSourcePathEmpty");
        Assert.Contains(messages, message => message.Code == "TextBindingStringFormatInvalid");
    }

    [Fact]
    public void TextBindingHelperCreatesOrderedMultiBinding()
    {
        var service = new Moq.Mock<ISharedDataService>().Object;
        var expression = new FrontedTextBindingExpression
        {
            Sources =
            [
                new FrontedBindingSourceConfig { Path = "HomeTeam.Name" },
                new FrontedBindingSourceConfig { Path = "AwayTeam.Name" }
            ]
        };

        var binding = FrontedTextBindingHelper.CreateMultiBinding(expression, service);

        Assert.Equal(2, binding.Bindings.Count);
        Assert.Equal("HomeTeam.Name", Assert.IsType<Binding>(binding.Bindings[0]).Path.Path);
        Assert.Equal("AwayTeam.Name", Assert.IsType<Binding>(binding.Bindings[1]).Path.Path);
    }

    private static string Convert(object[] values, FrontedTextBindingExpression expression) =>
        Assert.IsType<string>(new FrontedTextMultiBindingConverter().Convert(
            values,
            typeof(string),
            expression,
            CultureInfo.InvariantCulture));
}
