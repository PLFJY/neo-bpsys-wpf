using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Tests.Infrastructure;
using neo_bpsys_wpf.ViewModels.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using ShapePolygon = System.Windows.Shapes.Polygon;
using ShapeRectangle = System.Windows.Shapes.Rectangle;
using Xunit;

namespace neo_bpsys_wpf.Tests.Models;

public class TeamColorAndShapeTest
{
    [Fact]
    public void TeamColorDefaultsNormalizesAndImports()
    {
        var oldTeam = JsonSerializer.Deserialize<Team>("""{"Name":"Old","ImageUri":""}""");
        Assert.NotNull(oldTeam);
        Assert.Equal(Team.DefaultHomeColorHex, oldTeam.ColorHex);

        var team = new Team(Camp.Sur, TeamType.HomeTeam) { ColorHex = "#337fb9" };
        Assert.Equal("#FF337FB9", team.ColorHex);
        team.ColorHex = "#80abcdef";
        Assert.Equal("#80ABCDEF", team.ColorHex);
        team.ColorHex = "invalid";
        Assert.Equal("#80ABCDEF", team.ColorHex);

        var target = new Team(Camp.Hun, TeamType.AwayTeam);
        target.ImportTeamInfo(team);
        Assert.Equal("#80ABCDEF", target.ColorHex);
    }

    [Fact]
    public void ColorHelperParsesNormalizesAndCreatesBrush()
    {
        Assert.True(ColorHelper.TryNormalizeHex("#123456", out var rgb));
        Assert.Equal("#FF123456", rgb);
        Assert.True(ColorHelper.TryParseColor("#80123456", out var argb));
        Assert.Equal(Color.FromArgb(0x80, 0x12, 0x34, 0x56), argb);
        Assert.False(ColorHelper.TryNormalizeHex("bad", out _));
    }

    [Fact]
    public void ShapeDefaultsAndJsonRoundTrip()
    {
        var factory = new FrontedControlDefaultConfigFactory();
        var document = new FrontedCanvasDesignDocument
        {
            CanvasConfig = new FrontedCanvasConfig { CanvasWidth = 800, CanvasHeight = 600 }
        };
        var rectangle = Assert.IsType<RectangleFrontedControlConfig>(factory.Create("Rectangle", document));
        var polygon = Assert.IsType<PolygonFrontedControlConfig>(factory.Create("Polygon", document));

        Assert.Equal(3, polygon.Points.Count);

        var canvas = new FrontedCanvasConfig();
        canvas.Controls["Rect"] = rectangle;
        canvas.Controls["Poly"] = polygon;
        var roundTrip = JsonSerializer.Deserialize<FrontedCanvasConfig>(JsonSerializer.Serialize(canvas));
        Assert.IsType<RectangleFrontedControlConfig>(roundTrip!.Controls["Rect"]);
        Assert.IsType<PolygonFrontedControlConfig>(roundTrip.Controls["Poly"]);
    }

    [Fact]
    public void RectangleAndPolygonRenderSolidGradientAndBinding()
    {
        RunOnStaThread(() =>
        {
            var home = new Team(Camp.Sur, TeamType.HomeTeam) { ColorHex = "#FF123456" };
            var shared = new Mock<ISharedDataService>();
            shared.SetupGet(service => service.HomeTeam).Returns(home);
            var context = CreateContext(shared.Object);

            var boundRectangle = Assert.IsType<ShapeRectangle>(new RectangleFrontedControl().Create(
                "Bound",
                new RectangleFrontedControlConfig
                {
                    Width = 100,
                    Height = 50,
                    UseFillBinding = true,
                    FillBindingPath = "HomeTeam.ColorHex"
                },
                context));
            Assert.NotNull(BindingOperations.GetBinding(boundRectangle, ShapeRectangle.FillProperty));

            var gradientRectangle = Assert.IsType<ShapeRectangle>(new RectangleFrontedControl().Create(
                "Gradient",
                new RectangleFrontedControlConfig
                {
                    Width = 100,
                    Height = 50,
                    FillMode = ShapeFillMode.LinearGradient,
                    GradientStartColor = "#FF000000",
                    GradientEndColor = "#FFFFFFFF",
                    GradientAngle = 90
                },
                context));
            var gradient = Assert.IsType<LinearGradientBrush>(gradientRectangle.Fill);
            Assert.Equal(2, gradient.GradientStops.Count);
            Assert.Equal(new Point(0.5, 0), gradient.StartPoint);
            Assert.Equal(new Point(0.5, 1), gradient.EndPoint);

            var polygon = Assert.IsType<ShapePolygon>(new PolygonFrontedControl().Create(
                "Polygon",
                new PolygonFrontedControlConfig { Width = 200, Height = 100 },
                context));
            Assert.Equal(new Point(100, 0), polygon.Points[0]);
            Assert.IsType<SolidColorBrush>(polygon.Fill);

            var fallbackPolygon = Assert.IsType<ShapePolygon>(new PolygonFrontedControl().Create(
                "Fallback",
                new PolygonFrontedControlConfig { Width = 200, Height = 100, Points = [] },
                context));
            Assert.Equal(3, fallbackPolygon.Points.Count);
        });
    }

    [Fact]
    public void TextForegroundBindingUsesSharedDataColorAndOverridesStaticColor()
    {
        RunOnStaThread(() =>
        {
            var home = new Team(Camp.Sur, TeamType.HomeTeam) { ColorHex = "#FF123456" };
            var shared = new Mock<ISharedDataService>();
            shared.SetupGet(service => service.HomeTeam).Returns(home);

            var element = new TextFrontedControl().Create(
                "Title",
                new TextFrontedControlConfig
                {
                    Text = "Title",
                    Color = "#FFFFFFFF",
                    ColorBindingPath = "HomeTeam.ColorHex"
                },
                CreateContext(shared.Object));

            var border = Assert.IsType<Border>(element);
            var textBlock = Assert.IsType<TextBlock>(border.Child);
            var binding = BindingOperations.GetBinding(textBlock, TextBlock.ForegroundProperty);
            Assert.NotNull(binding);
            Assert.Equal("HomeTeam.ColorHex", binding.Path.Path);
        });
    }

    [Fact]
    public void PolygonGeometryAndVertexCommandsClampAndKeepThreePoints()
    {
        var config = new PolygonFrontedControlConfig
        {
            Left = 10,
            Top = 20,
            Width = 200,
            Height = 100
        };
        var canvasPoint = PolygonVertexGeometryHelper.ToCanvasPoint(config, new PolygonVertexConfig(0.5, 0.25));
        Assert.Equal(new Point(110, 45), canvasPoint);
        var normalized = PolygonVertexGeometryHelper.ToNormalizedPoint(config, new Point(500, -50));
        Assert.Equal(1, normalized.X);
        Assert.Equal(0, normalized.Y);

        var item = new FrontedControlDesignItem { Name = "Poly", Config = config };
        var viewModel = new FrontedDesignerWindowViewModel
        {
            CurrentDocument = new FrontedCanvasDesignDocument { Controls = { item } }
        };
        viewModel.SelectDesignItem(item);
        viewModel.RemovePolygonVertexCommand.Execute(null);
        Assert.Equal(3, config.Points.Count);
        viewModel.AddPolygonVertexCommand.Execute(null);
        Assert.Equal(4, config.Points.Count);
        Assert.True(viewModel.CurrentDocument.IsDirty);
        viewModel.RemovePolygonVertexCommand.Execute(null);
        Assert.Equal(3, config.Points.Count);
    }

    [Fact]
    public void BindingCatalogAndPropertyGridExposeShapeColorBindings()
    {
        var paths = FlattenCatalog(new FrontedBindingReflectionCatalogProvider().BuildCatalog())
            .Select(node => node.FullPath)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("HomeTeam.ColorHex", paths);
        Assert.Contains("AwayTeam.ColorHex", paths);

        var item = new FrontedControlDesignItem
        {
            Name = "Shape",
            Config = new PolygonFrontedControlConfig
            {
                UseGradient = true,
                FillBindingPath = "HomeTeam.ColorHex",
                GradientEndBindingPath = "AwayTeam.ColorHex"
            }
        };
        var document = new FrontedCanvasDesignDocument { Controls = { item } };
        var rows = new FrontedPropertyGridBuilder().Build(
            document,
            item,
            new FrontedLayoutValidator(),
            new FrontedLayoutReferenceScanner());

        Assert.DoesNotContain(rows, row => row.PropertyName == nameof(FrontedControlConfigBase.BindingPath));
        Assert.DoesNotContain(rows, row => row.PropertyName == nameof(ShapeFrontedControlConfigBase.FillMode));
        Assert.DoesNotContain(rows, row => row.PropertyName == nameof(ShapeFrontedControlConfigBase.GradientStartColor));
        Assert.DoesNotContain(rows, row => row.PropertyName == nameof(ShapeFrontedControlConfigBase.GradientStartBindingPath));
        Assert.DoesNotContain(rows, row => row.PropertyName == nameof(ShapeFrontedControlConfigBase.UseGradientStartBinding));
        Assert.DoesNotContain(rows, row => row.PropertyName == nameof(ShapeFrontedControlConfigBase.UseFillBinding));
        Assert.DoesNotContain(rows, row => row.PropertyName == nameof(ShapeFrontedControlConfigBase.UseGradientEndBinding));
        Assert.Contains(rows, row => row.PropertyName == nameof(ShapeFrontedControlConfigBase.UseGradient));
        // Color editors remain Color kind; validation warning handles the binding-active UX
        Assert.All(
            new[] { "FillColor", "GradientEndColor", "StrokeColor" },
            propertyName => Assert.Equal(
                FrontedPropertyEditorKind.Color,
                rows.Single(row => row.PropertyName == propertyName).EditorKind));
        Assert.All(
            new[] { "FillBindingPath", "GradientEndBindingPath" },
            propertyName => Assert.True(rows.Single(row => row.PropertyName == propertyName).CanBrowseBinding));
    }

    [Fact]
    public void PropertyGridExposesTextColorBindingAndMarksStaticColorIgnored()
    {
        var item = new FrontedControlDesignItem
        {
            Name = "Title",
            Config = new TextFrontedControlConfig
            {
                Color = "not-a-static-color",
                ColorBindingPath = "HomeTeam.ColorHex"
            }
        };
        var document = new FrontedCanvasDesignDocument { Controls = { item } };
        var rows = new FrontedPropertyGridBuilder().Build(
            document,
            item,
            new FrontedLayoutValidator(),
            new FrontedLayoutReferenceScanner());

        var bindingRow = rows.Single(row => row.PropertyName == nameof(TextFrontedControlConfig.ColorBindingPath));
        Assert.True(bindingRow.CanBrowseBinding);
        Assert.Equal(FrontedBindingTargetKind.String, bindingRow.BindingTargetKind);

        var colorRow = rows.Single(row => row.PropertyName == nameof(TextFrontedControlConfig.Color));
        Assert.Contains(colorRow.ValidationMessages, message => message.Code == "TextColorIgnored");
        Assert.DoesNotContain(colorRow.ValidationErrors, message => message.Contains("Invalid color", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidatorReportsInvalidShapeColorAndTooFewPolygonPoints()
    {
        var item = new FrontedControlDesignItem
        {
            Name = "Invalid",
            Config = new PolygonFrontedControlConfig { FillColor = "bad", Points = [] }
        };
        var messages = new FrontedLayoutValidator().Validate(
            new FrontedCanvasDesignDocument { Controls = { item } });

        Assert.Contains(messages, message => message.PropertyName == nameof(ShapeFrontedControlConfigBase.FillColor));
        Assert.Contains(messages, message => message.PropertyName == nameof(PolygonFrontedControlConfig.Points));
    }

    private static FrontedControlBuildContext CreateContext(ISharedDataService sharedDataService) =>
        new()
        {
            Services = new Mock<IServiceProvider>().Object,
            SharedDataService = sharedDataService,
            ResourceResolver = new Mock<IFrontedResourceResolver>().Object,
            WindowId = "TestWindow",
            CanvasName = "BaseCanvas",
            Logger = NullLogger.Instance
        };

    private static IEnumerable<FrontedBindingTreeNode> FlattenCatalog(IEnumerable<FrontedBindingTreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in FlattenCatalog(node.Children))
            {
                yield return child;
            }
        }
    }

    private static void RunOnStaThread(Action action)
    {
        WpfTestThread.Run(action);
    }
}
