#pragma warning disable CS1591

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Converters;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using neo_bpsys_wpf.Core.Models.Legacy;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows.Media;
using static neo_bpsys_wpf.Core.Services.FrontedLayout.LegacyConvertMessageHelper;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 旧版前台布局包转换器的Models逻辑。
/// </summary>
public sealed partial class FrontedLayoutPackageLegacyConverter
{
    private sealed class ResourceConvertState(string packageId)
    {
        public string PackageId { get; } = packageId;

        public List<FrontedLayoutPackageResourceEntry> Resources { get; } = [];

        public Dictionary<string, string> ByFileName { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string> ByLegacyRelativePath { get; } = new(StringComparer.OrdinalIgnoreCase);

        public void Add(string sourcePath, string uri, string relativePath, string kind, string sha256, string safeName)
        {
            Resources.Add(new FrontedLayoutPackageResourceEntry
            {
                Id = Path.GetFileNameWithoutExtension(safeName),
                Kind = kind,
                Path = relativePath,
                Uri = uri,
                Sha256 = sha256
            });

            ByFileName[Path.GetFileName(sourcePath)] = uri;
            ByLegacyRelativePath[Path.GetFileName(sourcePath)] = uri;
            ByLegacyRelativePath[$"CustomUi/{Path.GetFileName(sourcePath)}"] = uri;
        }
    }

    private sealed record ScoreGlobalCell(
        string ControlName,
        string Team,
        int Game,
        string Half,
        bool IsOvertime,
        ElementInfo Info);

    private sealed record LegacyScoreGlobalCellBlueprint(
        string Team,
        int Game,
        string Half,
        bool IsOvertime);

    private readonly record struct LegacyLayoutKey(string SourceWindow, string SourceCanvas);

    private enum LegacyControlBlueprintStatus
    {
        Mapped,
        Folded,
        Aggregated,
        Unsupported,
        RemovedWithReason
    }

    private sealed record LegacyControlBlueprint
    {
        public string SourceWindow { get; init; } = string.Empty;

        public string SourceCanvas { get; init; } = string.Empty;

        public string LegacyName { get; init; } = string.Empty;

        public string? TargetWindow { get; init; }

        public string TargetName { get; init; } = string.Empty;

        public string TargetControlType { get; init; } = string.Empty;

        public string? BindingPath { get; init; }

        public string? TextBinding { get; init; }

        public string? StaticText { get; init; }

        public string? FontFamily { get; init; }

        public double? FontSize { get; init; }

        public string? FontWeight { get; init; }

        public string? Color { get; init; }

        public string? HorizontalAlignment { get; init; }

        public string? VerticalAlignment { get; init; }

        public string? TextAlignment { get; init; }

        public string? TextWrapping { get; init; }

        public double ContentMarginLeft { get; init; }

        public double ContentMarginTop { get; init; }

        public double ContentMarginRight { get; init; }

        public double ContentMarginBottom { get; init; }

        public string? ImageBindingPath { get; init; }

        public string? ImagePath { get; init; }

        public ImageSizingMode? SizingMode { get; init; }

        public string? Stretch { get; init; }

        public bool ClipToBounds { get; init; }

        public double? CornerRadius { get; init; }

        public int ZIndex { get; init; }

        public double? DefaultLeft { get; init; }

        public double? DefaultTop { get; init; }

        public double? DefaultWidth { get; init; }

        public double? DefaultHeight { get; init; }

        public IReadOnlyDictionary<string, string> SpecialProperties { get; init; } =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public string? TextStyleSourceKey { get; init; }

        public string? ResourceSourceKey { get; init; }

        public LegacyControlBlueprintStatus Status { get; init; } = LegacyControlBlueprintStatus.Mapped;

        public string? UnsupportedReason { get; init; }

        public bool Required { get; init; } = true;
    }

    private sealed record LegacyWindowDefaults(
        double WindowWidth,
        double WindowHeight,
        double CanvasWidth,
        double CanvasHeight,
        string? BackgroundImage);

    private sealed record LegacyTextStyleDefaults(
        string? HorizontalAlignment,
        string? VerticalAlignment,
        string? TextAlignment,
        string? TextWrapping,
        string? FontFamily,
        string? FontWeight,
        string? Color,
        double FontSize);

    private sealed record LegacyLayoutMapping(
        string SourceWindow,
        string SourceCanvas,
        string? TargetWindow,
        double? FixedCanvasWidth = null,
        double? FixedCanvasHeight = null)
    {
        public bool IsSupported => !string.IsNullOrWhiteSpace(TargetWindow);

        public string TargetLayoutPath => ToZipPath("FrontedLayouts", $"{TargetWindow}.json");

        public static LegacyLayoutMapping Unsupported(string sourceWindow, string sourceCanvas)
        {
            return new LegacyLayoutMapping(sourceWindow, sourceCanvas, null);
        }
    }

    private readonly record struct PaintedBounds(double MinX, double MinY, double MaxX, double MaxY)
    {
        public static PaintedBounds? From(IEnumerable<ElementInfo> elements)
        {
            var hasAny = false;
            var minX = double.PositiveInfinity;
            var minY = double.PositiveInfinity;
            var maxX = double.NegativeInfinity;
            var maxY = double.NegativeInfinity;

            foreach (var element in elements)
            {
                if (!element.Left.HasValue || !element.Top.HasValue)
                {
                    continue;
                }

                var width = element.Width.GetValueOrDefault();
                var height = element.Height.GetValueOrDefault();
                if (width < 0D || height < 0D)
                {
                    continue;
                }

                var left = element.Left.Value;
                var top = element.Top.Value;
                minX = Math.Min(minX, left);
                minY = Math.Min(minY, top);
                maxX = Math.Max(maxX, left + width);
                maxY = Math.Max(maxY, top + height);
                hasAny = true;
            }

            return hasAny ? new PaintedBounds(minX, minY, maxX, maxY) : null;
        }
    }
}

#pragma warning restore CS1591
