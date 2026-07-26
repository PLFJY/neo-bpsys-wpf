using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Options;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Abstractions.Services;
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
using System.Windows.Media.Imaging;
using Xunit;

namespace neo_bpsys_wpf.Tests.Models;

public class BackgroundTintFrontedControlTest
{
    [Fact]
    public void DefaultsJsonPropertyGridAndValidationSupportTintControls()
    {
        var factory = new FrontedControlDefaultConfigFactory(CreateTintV3Registry());
        var document = new FrontedCanvasDesignDocument
        {
            CanvasConfig = new FrontedCanvasConfig { CanvasWidth = 800, CanvasHeight = 600 }
        };
        var rectangle = Assert.IsType<BackgroundTintRectangleFrontedControlConfig>(
            factory.Create("BackgroundTintRectangle", document));
        var polygon = Assert.IsType<BackgroundTintPolygonFrontedControlConfig>(
            factory.Create("BackgroundTintPolygon", document));
        Assert.Equal(3, polygon.Points.Count);
        Assert.Equal(0.45D, polygon.TextureStrength);

        rectangle.TintMode = BackgroundTintMode.BaseColorWithTexture;
        rectangle.TextureStrength = 0.7D;
        var canvas = new FrontedCanvasConfig();
        canvas.Controls["TintRect"] = rectangle;
        canvas.Controls["TintPoly"] = polygon;
        var roundTrip = JsonSerializer.Deserialize<FrontedCanvasConfig>(JsonSerializer.Serialize(canvas));
        var roundTripRectangle = Assert.IsType<BackgroundTintRectangleFrontedControlConfig>(roundTrip.Controls["TintRect"]);
        Assert.Equal(0.7D, roundTripRectangle.TextureStrength);

        var item = new FrontedControlDesignItem { Name = "Tint", Config = polygon };
        document.Controls.Add(item);
        var rows = new FrontedPropertyGridBuilder().Build(
            document,
            item,
            new FrontedLayoutValidator(),
            new FrontedLayoutReferenceScanner());
        Assert.Equal(
            FrontedPropertyEditorKind.Color,
            rows.Single(row => row.PropertyName == nameof(polygon.TintColor)).EditorKind);
        Assert.True(rows.Single(row => row.PropertyName == nameof(polygon.TintBindingPath)).CanBrowseBinding);
        Assert.Equal(
            FrontedPropertyEditorKind.Number,
            rows.Single(row => row.PropertyName == nameof(polygon.TextureStrength)).EditorKind);
        Assert.DoesNotContain(rows, row => row.PropertyName == nameof(FrontedControlConfigBase.BindingPath));

        polygon.TintColor = "bad";
        polygon.TintStrength = 2D;
        polygon.TextureStrength = double.PositiveInfinity;
        polygon.Points = [];
        var messages = new FrontedLayoutValidator().Validate(document);
        Assert.Contains(messages, message => message.PropertyName == nameof(polygon.TintColor));
        Assert.Contains(messages, message => message.PropertyName == nameof(polygon.TintStrength));
        Assert.Contains(messages, message => message.PropertyName == nameof(polygon.TextureStrength));
        Assert.Contains(messages, message => message.PropertyName == nameof(polygon.Points));
    }

    [Fact]
    public void BaseColorWithTexturePreservesAlphaAndKeepsAverageCloseToTint()
    {
        RunOnStaThread(() =>
        {
            var source = CreateGrayTexture(
                (80, 40),
                (100, 80),
                (120, 160),
                (140, 220));
            var tint = Color.FromRgb(0x33, 0x80, 0xB9);
            var result = new BackgroundImageTintProcessor().CreateTinted(
                source,
                "texture",
                tint,
                BackgroundTintMode.BaseColorWithTexture,
                1D,
                0.45D)!;
            var pixels = ReadPixels(result);

            Assert.Equal(new byte[] { 40, 80, 160, 220 }, pixels.Select(pixel => pixel.A).ToArray());
            Assert.InRange(pixels.Average(pixel => pixel.R), tint.R - 12D, tint.R + 12D);
            Assert.InRange(pixels.Average(pixel => pixel.G), tint.G - 12D, tint.G + 12D);
            Assert.InRange(pixels.Average(pixel => pixel.B), tint.B - 12D, tint.B + 12D);
        });
    }

    [Fact]
    public void BaseColorWithTextureStrengthControlsTextureContrast()
    {
        RunOnStaThread(() =>
        {
            var source = CreateGrayTexture((70, 255), (100, 255), (130, 255), (160, 255));
            var tint = Color.FromRgb(0x33, 0x80, 0xB9);
            var processor = new BackgroundImageTintProcessor();
            var flat = processor.CreateTinted(
                source, "flat", tint, BackgroundTintMode.BaseColorWithTexture, 1D, 0D)!;
            Assert.All(ReadPixels(flat), pixel =>
            {
                Assert.Equal(tint.R, pixel.R);
                Assert.Equal(tint.G, pixel.G);
                Assert.Equal(tint.B, pixel.B);
                Assert.Equal(255, pixel.A);
            });

            var subtle = processor.CreateTinted(
                source, "subtle", tint, BackgroundTintMode.BaseColorWithTexture, 1D, 0.1D)!;
            var strong = processor.CreateTinted(
                source, "strong", tint, BackgroundTintMode.BaseColorWithTexture, 1D, 0.8D)!;
            Assert.True(LuminanceStandardDeviation(ReadPixels(strong))
                        > LuminanceStandardDeviation(ReadPixels(subtle)));
        });
    }

    [Fact]
    public void BaseColorWithTextureUsesLocalRectangleMeanAndSeparatesCacheEntries()
    {
        RunOnStaThread(() =>
        {
            var source = CreateGrayTexture((20, 255), (40, 255), (200, 255), (220, 180));
            var tint = Color.FromRgb(0x33, 0x80, 0xB9);
            var processor = new BackgroundImageTintProcessor();
            var localOptions = CreateProcessingOptions(
                new Rect(2, 0, 2, 1),
                BackgroundTintNormalizationMode.VisibleRectangle);
            var local = processor.CreateTinted(source, "split", tint, localOptions)!;
            var localPixels = ReadPixels(local).Skip(2).ToArray();

            Assert.InRange(localPixels.Average(pixel => pixel.R), tint.R - 2D, tint.R + 2D);
            Assert.InRange(localPixels.Average(pixel => pixel.G), tint.G - 2D, tint.G + 2D);
            Assert.InRange(localPixels.Average(pixel => pixel.B), tint.B - 2D, tint.B + 2D);
            Assert.Equal(180, localPixels[1].A);

            var whole = processor.CreateTinted(
                source,
                "split",
                tint,
                CreateProcessingOptions(
                    new Rect(0, 0, 4, 1),
                    BackgroundTintNormalizationMode.WholeImage))!;
            var wholePixels = ReadPixels(whole).Skip(2).ToArray();
            Assert.True(ColorDistance(localPixels, tint) < ColorDistance(wholePixels, tint));
            Assert.NotSame(local, whole);

            var darkLocal = processor.CreateTinted(
                source,
                "split",
                tint,
                CreateProcessingOptions(
                    new Rect(0, 0, 2, 1),
                    BackgroundTintNormalizationMode.VisibleRectangle))!;
            Assert.NotSame(local, darkLocal);
            Assert.NotEqual(ReadPixels(local)[0], ReadPixels(darkLocal)[0]);
            var darkLocalPixels = ReadPixels(darkLocal).Take(2).ToArray();
            Assert.InRange(darkLocalPixels.Average(pixel => pixel.R), tint.R - 2D, tint.R + 2D);
            Assert.InRange(darkLocalPixels.Average(pixel => pixel.G), tint.G - 2D, tint.G + 2D);
            Assert.InRange(darkLocalPixels.Average(pixel => pixel.B), tint.B - 2D, tint.B + 2D);
        });
    }

    [Fact]
    public void BaseColorWithTextureUsesLocalPolygonMean()
    {
        RunOnStaThread(() =>
        {
            var source = CreateGrayTexture2D(
                4,
                2,
                (20, 255), (30, 255), (200, 255), (220, 255),
                (40, 255), (50, 255), (180, 255), (200, 255));
            var tint = Color.FromRgb(0x33, 0x80, 0xB9);
            var options = CreateProcessingOptions(
                new Rect(2, 0, 2, 2),
                BackgroundTintNormalizationMode.VisiblePolygon,
                [
                    new PolygonVertexConfig { X = 0, Y = 0 },
                    new PolygonVertexConfig { X = 1, Y = 0 },
                    new PolygonVertexConfig { X = 1, Y = 1 },
                    new PolygonVertexConfig { X = 0, Y = 1 }
                ],
                canvasWidth: 4,
                canvasHeight: 2);
            var result = new BackgroundImageTintProcessor().CreateTinted(source, "polygon", tint, options)!;
            var pixels = ReadPixels(result);
            var visible = new[] { pixels[2], pixels[3], pixels[6], pixels[7] };

            Assert.InRange(visible.Average(pixel => pixel.R), tint.R - 2D, tint.R + 2D);
            Assert.InRange(visible.Average(pixel => pixel.G), tint.G - 2D, tint.G + 2D);
            Assert.InRange(visible.Average(pixel => pixel.B), tint.B - 2D, tint.B + 2D);
        });
    }

    [Fact]
    public void LocalRectangleTextureStrengthZeroProducesFlatTintAndStrengthControlsContrast()
    {
        RunOnStaThread(() =>
        {
            var source = CreateGrayTexture((10, 255), (30, 255), (180, 255), (240, 120));
            var tint = Color.FromRgb(0x33, 0x80, 0xB9);
            var processor = new BackgroundImageTintProcessor();
            var flat = processor.CreateTinted(
                source,
                "local-flat",
                tint,
                CreateProcessingOptions(new Rect(2, 0, 2, 1), textureStrength: 0D))!;
            Assert.All(ReadPixels(flat).Skip(2), pixel =>
            {
                Assert.Equal(tint.R, pixel.R);
                Assert.Equal(tint.G, pixel.G);
                Assert.Equal(tint.B, pixel.B);
            });
            Assert.Equal(120, ReadPixels(flat)[3].A);

            var subtle = processor.CreateTinted(
                source,
                "local-subtle",
                tint,
                CreateProcessingOptions(new Rect(2, 0, 2, 1), textureStrength: 0.1D))!;
            var strong = processor.CreateTinted(
                source,
                "local-strong",
                tint,
                CreateProcessingOptions(new Rect(2, 0, 2, 1), textureStrength: 0.8D))!;
            Assert.True(
                LuminanceStandardDeviation(ReadPixels(strong).Skip(2))
                > LuminanceStandardDeviation(ReadPixels(subtle).Skip(2)));
        });
    }

    [Fact]
    public void InvalidTextureStrengthDoesNotCrash()
    {
        RunOnStaThread(() =>
        {
            var source = CreateGrayTexture((90, 255), (120, 255));
            var processor = new BackgroundImageTintProcessor();
            Assert.NotNull(processor.CreateTinted(
                source, "nan", Colors.CornflowerBlue, BackgroundTintMode.BaseColorWithTexture, 1D, double.NaN));
            Assert.NotNull(processor.CreateTinted(
                source, "infinity", Colors.CornflowerBlue, BackgroundTintMode.BaseColorWithTexture, 1D, double.PositiveInfinity));
            Assert.NotNull(processor.CreateTinted(
                source, "range", Colors.CornflowerBlue, BackgroundTintMode.BaseColorWithTexture, 1D, 5D));
        });
    }

    [Fact]
    public void TintProcessorPreservesAlphaAndAppliesModesAndStrength()
    {
        RunOnStaThread(() =>
        {
            var source = CreateBitmap(10, 20, 30, 77);
            var processor = new BackgroundImageTintProcessor();

            var original = processor.CreateTinted(
                source, "source", Colors.Red, BackgroundTintMode.LuminanceColorize, 0)!;
            Assert.Equal(new byte[] { 30, 20, 10, 77 }, ReadPixel(original));

            var colorized = processor.CreateTinted(
                source, "source", Colors.Red, BackgroundTintMode.LuminanceColorize, 1)!;
            var colorizedPixel = ReadPixel(colorized);
            Assert.Equal(77, colorizedPixel[3]);
            Assert.Equal(0, colorizedPixel[0]);
            Assert.Equal(0, colorizedPixel[1]);
            Assert.True(colorizedPixel[2] > 0);

            var multiplied = processor.CreateTinted(
                source, "source", Color.FromRgb(255, 0, 255), BackgroundTintMode.Multiply, 1)!;
            Assert.Equal(new byte[] { 30, 0, 10, 77 }, ReadPixel(multiplied));
            Assert.Same(
                multiplied,
                processor.CreateTinted(source, "source", Color.FromRgb(255, 0, 255), BackgroundTintMode.Multiply, 1));
        });
    }

    [Fact]
    public void TintControlsAlignClipBindAndHandleMissingBackground()
    {
        RunOnStaThread(() =>
        {
            var home = new Team(Camp.Sur, TeamType.HomeTeam) { ColorHex = "#FFFF0000" };
            var shared = new Mock<ISharedDataService>();
            shared.SetupGet(service => service.HomeTeam).Returns(home);
            var resolver = new Mock<IFrontedResourceResolver>();
            resolver.Setup(service => service.ResolveImage("Resources/bg.png", FrontedImagePurpose.Background))
                .Returns(CreateBitmap(100, 100, 100, 200));
            var rectangleConfig = new BackgroundTintRectangleFrontedControlConfig
            {
                Left = 12,
                Top = 34,
                Width = 160,
                Height = 80,
                RadiusX = 8,
                RadiusY = 9,
                TintBindingPath = "HomeTeam.ColorHex",
                TintMode = BackgroundTintMode.BaseColorWithTexture
            };
            var rectangleControl = new BackgroundTintRectangleFrontedControl();
            rectangleControl.InitializeFrontedV3(
                CreateV3Context(rectangleConfig, shared.Object, resolver.Object));
            var rectangle = Assert.IsType<BackgroundTintControlHost>(rectangleControl.Content);
            Assert.IsType<RectangleGeometry>(rectangle.Clip);

            home.ColorHex = "#FF00FF00";
            rectangle.GetBindingExpression(BackgroundTintControlHost.TintColorValueProperty)?.UpdateTarget();
            Assert.Equal("#FF00FF00", rectangle.TintColorValue);

            var polygonConfig = new BackgroundTintPolygonFrontedControlConfig
            {
                Left = 20,
                Top = 10,
                Width = 200,
                Height = 100,
                TintMode = BackgroundTintMode.BaseColorWithTexture
            };
            var geometry = BackgroundTintPolygonFrontedControl.CreateGeometry(polygonConfig);
            Assert.Equal(new Point(100, 0), geometry.Figures[0].StartPoint);
            polygonConfig.Points = [];

            var missingLiveControl = new BackgroundTintRectangleFrontedControl();
            missingLiveControl.InitializeFrontedV3(
                CreateV3Context(rectangleConfig, shared.Object, new Mock<IFrontedResourceResolver>().Object));
            Assert.Empty(Assert.IsType<Grid>(missingLiveControl.Content).Children);

            var missingDesignerControl = new BackgroundTintRectangleFrontedControl();
            missingDesignerControl.InitializeFrontedV3(
                CreateV3Context(rectangleConfig, shared.Object, new Mock<IFrontedResourceResolver>().Object, true));
            Assert.NotEmpty(Assert.IsType<Grid>(missingDesignerControl.Content).Children);
        });
    }

    [Fact]
    public void RendererPassesEffectiveBo3BackgroundAndCanvasSizeToControl()
    {
        RunOnStaThread(() =>
        {
            var shared = new Mock<ISharedDataService>();
            shared.SetupGet(service => service.IsBo3Mode).Returns(true);
            var renderer = new FrontedRenderer(
                new Mock<IServiceProvider>().Object,
                shared.Object,
                new Mock<IFrontedResourceResolver>().Object,
                new FrontedV3ControlRegistry([CreateRecordingRegistration()]),
                NullLogger<FrontedRenderer>.Instance);
            var canvas = new Canvas();
            renderer.RenderToCanvas(
                canvas,
                new FrontedCanvasConfig
                {
                    CanvasWidth = 123,
                    CanvasHeight = 456,
                    EnableBoModeStates = true,
                    BoModeStates =
                    {
                        ["Bo3"] = new FrontedCanvasStateConfig
                        {
                            BackgroundImage = "Resources/bo3.png",
                            Controls = { ["Recorder"] = new RecordingConfig() }
                        }
                    }
                },
                new FrontedRenderContext
                {
                    WindowId = "Window",
                    CanvasName = "Canvas",
                    IsDesignerPreview = true
                });

            Assert.True(RecordingV3Control.LastContext?.IsDesignerPreview);
        });
    }

    [Fact]
    public void TintPolygonReusesDesignerVertexEditing()
    {
        var config = new BackgroundTintPolygonFrontedControlConfig();
        var item = new FrontedControlDesignItem { Name = "TintPolygon", Config = config };
        var viewModel = new FrontedDesignerWindowViewModel
        {
            CurrentDocument = new FrontedCanvasDesignDocument { Controls = { item } }
        };
        viewModel.SelectDesignItem(item);
        Assert.True(viewModel.IsPolygonSelected);
        viewModel.AddPolygonVertexCommand.Execute(null);
        Assert.Equal(4, config.Points.Count);
        viewModel.RemovePolygonVertexCommand.Execute(null);
        Assert.Equal(3, config.Points.Count);
    }

    private static FrontedV3ControlContext CreateV3Context(
        FrontedControlConfigBase config,
        ISharedDataService shared,
        IFrontedResourceResolver resolver,
        bool isDesigner = false) =>
        new()
        {
            Services = new ServiceCollection()
                .AddSingleton(new BackgroundImageTintProcessor())
                .BuildServiceProvider(),
            SharedDataService = shared,
            ResourceResolver = resolver,
            WindowId = "Window",
            CanvasName = "Canvas",
            Config = config,
            ControlName = "Test",
            CanvasBackgroundImage = "Resources/bg.png",
            CanvasWidth = 800,
            CanvasHeight = 600,
            IsDesignerPreview = isDesigner,
            Logger = NullLogger.Instance
        };

    private static BitmapSource CreateBitmap(byte r, byte g, byte b, byte a) =>
        BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { b, g, r, a }, 4);

    private static byte[] ReadPixel(BitmapSource source)
    {
        var pixel = new byte[4];
        source.CopyPixels(pixel, 4, 0);
        return pixel;
    }

    private static BitmapSource CreateGrayTexture(params (byte Luminance, byte Alpha)[] values)
    {
        var pixels = values
            .SelectMany(value => new[] { value.Luminance, value.Luminance, value.Luminance, value.Alpha })
            .ToArray();
        return BitmapSource.Create(values.Length, 1, 96, 96, PixelFormats.Bgra32, null, pixels, values.Length * 4);
    }

    private static BitmapSource CreateGrayTexture2D(
        int width,
        int height,
        params (byte Luminance, byte Alpha)[] values)
    {
        var pixels = values
            .SelectMany(value => new[] { value.Luminance, value.Luminance, value.Luminance, value.Alpha })
            .ToArray();
        return BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
    }

    private static BackgroundTintProcessingOptions CreateProcessingOptions(
        Rect region,
        BackgroundTintNormalizationMode normalizationMode = BackgroundTintNormalizationMode.VisibleRectangle,
        IReadOnlyList<PolygonVertexConfig> polygonPoints = null,
        double textureStrength = 0.45D,
        double canvasWidth = 4D,
        double canvasHeight = 1D) =>
        new()
        {
            Mode = BackgroundTintMode.BaseColorWithTexture,
            TintStrength = 1D,
            TextureStrength = textureStrength,
            NormalizationMode = normalizationMode,
            CanvasRegion = region,
            CanvasWidth = canvasWidth,
            CanvasHeight = canvasHeight,
            PolygonPoints = polygonPoints
        };

    private static (byte R, byte G, byte B, byte A)[] ReadPixels(BitmapSource source)
    {
        var stride = source.PixelWidth * 4;
        var pixels = new byte[stride * source.PixelHeight];
        source.CopyPixels(pixels, stride, 0);
        return Enumerable.Range(0, source.PixelWidth * source.PixelHeight)
            .Select(index =>
            {
                var offset = index * 4;
                return (pixels[offset + 2], pixels[offset + 1], pixels[offset], pixels[offset + 3]);
            })
            .ToArray();
    }

    private static double LuminanceStandardDeviation(IEnumerable<(byte R, byte G, byte B, byte A)> pixels)
    {
        var luminances = pixels
            .Select(pixel => pixel.R * 0.2126D + pixel.G * 0.7152D + pixel.B * 0.0722D)
            .ToArray();
        var average = luminances.Average();
        return Math.Sqrt(luminances.Average(value => Math.Pow(value - average, 2D)));
    }

    private static double ColorDistance(IEnumerable<(byte R, byte G, byte B, byte A)> pixels, Color tint)
    {
        var values = pixels.ToArray();
        return Math.Abs(values.Average(pixel => pixel.R) - tint.R)
               + Math.Abs(values.Average(pixel => pixel.G) - tint.G)
               + Math.Abs(values.Average(pixel => pixel.B) - tint.B);
    }

    private static void RunOnStaThread(Action action)
    {
        WpfTestThread.Run(action);
    }

    private sealed class RecordingConfig : FrontedControlConfigBase
    {
        public RecordingConfig()
        {
            ControlType = "Recording";
        }
    }

    [FrontedV3Control("Recording", IsBuiltIn = true)]
    private sealed class RecordingV3Control : FrontedV3ControlBase
    {
        public static FrontedV3ControlContext? LastContext { get; private set; }

        protected override void OnInitializeFrontedV3(FrontedV3ControlContext context)
        {
            LastContext = context;
            Content = new Grid();
        }
    }

    private static FrontedV3ControlRegistration CreateRecordingRegistration()
    {
        return new FrontedV3ControlRegistration
        {
            CanonicalControlType = "Recording",
            LocalControlId = "Recording",
            PackageId = "builtin",
            IsBuiltIn = true,
            ControlType = typeof(RecordingV3Control),
            ConfigType = typeof(RecordingConfig),
            Properties = Array.Empty<FrontedV3PropertyDefinition>(),
            CreateDefaultConfig = () => new RecordingConfig()
        };
    }

    private static FrontedV3ControlRegistry CreateTintV3Registry()
    {
        return new FrontedV3ControlRegistry([
            new FrontedV3ControlRegistration
            {
                CanonicalControlType = "BackgroundTintRectangle",
                LocalControlId = "BackgroundTintRectangle",
                PackageId = "builtin",
                IsBuiltIn = true,
                ControlType = typeof(BackgroundTintRectangleFrontedControl),
                ConfigType = typeof(BackgroundTintRectangleFrontedControlConfig),
                Properties = Array.Empty<FrontedV3PropertyDefinition>(),
                CreateDefaultConfig = () => new BackgroundTintRectangleFrontedControlConfig()
            },
            new FrontedV3ControlRegistration
            {
                CanonicalControlType = "BackgroundTintPolygon",
                LocalControlId = "BackgroundTintPolygon",
                PackageId = "builtin",
                IsBuiltIn = true,
                ControlType = typeof(BackgroundTintPolygonFrontedControl),
                ConfigType = typeof(BackgroundTintPolygonFrontedControlConfig),
                Properties = Array.Empty<FrontedV3PropertyDefinition>(),
                CreateDefaultConfig = () => new BackgroundTintPolygonFrontedControlConfig()
            }
        ]);
    }
}
