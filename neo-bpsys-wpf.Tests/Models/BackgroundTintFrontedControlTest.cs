using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.ViewModels.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Threading;
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
        var factory = new FrontedControlDefaultConfigFactory();
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
        Assert.IsType<BackgroundTintRectangleFrontedControlConfig>(roundTrip!.Controls["TintRect"]);
        Assert.IsType<BackgroundTintPolygonFrontedControlConfig>(roundTrip.Controls["TintPoly"]);
        var roundTripRectangle = Assert.IsType<BackgroundTintRectangleFrontedControlConfig>(roundTrip.Controls["TintRect"]);
        Assert.Equal(BackgroundTintMode.BaseColorWithTexture, roundTripRectangle.TintMode);
        Assert.Equal(0.7D, roundTripRectangle.TextureStrength);

        var item = new FrontedControlDesignItem { Name = "Tint", Config = polygon };
        document.Controls.Add(item);
        var rows = new FrontedPropertyGridBuilder().Build(
            document,
            item,
            new FrontedLayoutValidator(),
            new FrontedLayoutReferenceScanner(),
            new FrontedLayoutRuntimeContractCatalog());
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
        Assert.Contains(messages, message => message.Code == "MissingCanvasBackgroundImage");
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
            var context = CreateContext(shared.Object, resolver.Object);
            var rectangleConfig = new BackgroundTintRectangleFrontedControlConfig
            {
                Left = 12,
                Top = 34,
                Width = 160,
                Height = 80,
                RadiusX = 8,
                RadiusY = 9,
                TintBindingPath = "HomeTeam.ColorHex"
            };
            var rectangle = Assert.IsType<BackgroundTintControlHost>(
                new BackgroundTintRectangleFrontedControl().Create("Tint", rectangleConfig, context));
            Assert.Equal(new Thickness(-12, -34, 0, 0), rectangle.TintedImage.Margin);
            Assert.Equal(800, rectangle.TintedImage.Width);
            Assert.Equal(600, rectangle.TintedImage.Height);
            var clip = Assert.IsType<RectangleGeometry>(rectangle.Clip);
            Assert.Equal(160, clip.Rect.Width);
            Assert.Equal(8, clip.RadiusX);
            Assert.NotNull(BindingOperations.GetBinding(rectangle, BackgroundTintControlHost.TintColorValueProperty));

            home.ColorHex = "#FF00FF00";
            rectangle.GetBindingExpression(BackgroundTintControlHost.TintColorValueProperty)?.UpdateTarget();
            Assert.Equal("#FF00FF00", rectangle.TintColorValue);

            var polygonConfig = new BackgroundTintPolygonFrontedControlConfig { Width = 200, Height = 100 };
            var geometry = BackgroundTintPolygonFrontedControl.CreateGeometry(polygonConfig);
            Assert.Equal(new Point(100, 0), geometry.Figures[0].StartPoint);
            polygonConfig.Points = [];
            Assert.Equal(3, BackgroundTintPolygonFrontedControl.CreateGeometry(polygonConfig).Figures[0].Segments.Count + 2);

            var missingLive = new BackgroundTintRectangleFrontedControl().Create(
                "Missing",
                rectangleConfig,
                CreateContext(shared.Object, new Mock<IFrontedResourceResolver>().Object));
            Assert.Empty(Assert.IsType<Grid>(missingLive).Children);

            var designerContext = CreateContext(shared.Object, new Mock<IFrontedResourceResolver>().Object, true);
            var missingDesigner = Assert.IsType<Grid>(
                new BackgroundTintRectangleFrontedControl().Create("Missing", rectangleConfig, designerContext));
            Assert.NotEmpty(missingDesigner.Children);
        });
    }

    [Fact]
    public void RendererPassesEffectiveBo3BackgroundAndCanvasSizeToControl()
    {
        RunOnStaThread(() =>
        {
            var shared = new Mock<ISharedDataService>();
            shared.SetupGet(service => service.IsBo3Mode).Returns(true);
            var recorder = new RecordingControl();
            var renderer = new FrontedRenderer(
                new Mock<IServiceProvider>().Object,
                shared.Object,
                new Mock<IFrontedResourceResolver>().Object,
                new FrontedControlRegistry([recorder]),
                NullLogger<FrontedRenderer>.Instance);
            renderer.RenderToCanvas(
                new Canvas(),
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

            Assert.Equal("Resources/bo3.png", recorder.Context!.CanvasBackgroundImage);
            Assert.Equal(123, recorder.Context.CanvasWidth);
            Assert.Equal(456, recorder.Context.CanvasHeight);
            Assert.True(recorder.Context.IsDesignerPreview);
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

    private static FrontedControlBuildContext CreateContext(
        ISharedDataService shared,
        IFrontedResourceResolver resolver,
        bool isDesigner = false) =>
        new()
        {
            Services = new Mock<IServiceProvider>().Object,
            SharedDataService = shared,
            ResourceResolver = resolver,
            WindowId = "Window",
            CanvasName = "Canvas",
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

    private static void RunOnStaThread(Action action)
    {
        ExceptionDispatchInfo exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ExceptionDispatchInfo.Capture(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        exception?.Throw();
    }

    private sealed class RecordingConfig : FrontedControlConfigBase
    {
        public RecordingConfig()
        {
            ControlType = "Recording";
        }
    }

    private sealed class RecordingControl : IFrontedControl
    {
        public string ControlType => "Recording";
        public Type ConfigType => typeof(RecordingConfig);
        public FrontedControlBuildContext Context { get; private set; }

        public FrameworkElement Create(string name, FrontedControlConfigBase config, FrontedControlBuildContext context)
        {
            Context = context;
            return new Grid();
        }
    }
}
