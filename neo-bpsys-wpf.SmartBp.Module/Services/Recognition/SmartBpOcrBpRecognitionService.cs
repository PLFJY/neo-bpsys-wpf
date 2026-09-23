using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace neo_bpsys_wpf.SmartBp.Module.Services.Recognition;

internal sealed class SmartBpOcrBpRecognitionService(
    IOcrService ocr,
    ISmartBpRecognitionFrameCropper cropper,
    ISmartBpOcrContactSheetBuilder contactSheetBuilder,
    ISmartBpRecognitionSettingsService settings,
    ISmartBpBusinessStateMerger merger,
    SmartBpOcrRegionParser parser,
    ISmartBpLifecycleStatusDetector lifecycleDetector) : ISmartBpOcrBpRecognitionService
{
    public async Task<SmartBpOcrRecognitionResult> RecognizeAsync(
        BitmapSource frame,
        SmartBpOcrRecognitionRequest request,
        CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<string>();
        diagnostics.Add($"OCR provider selected: {ocr.SelectedProvider}; fallback=false.");
        var requestedRegions = BuildRequestedRegions(request);
        var regionTexts = settings.Settings.UseOcrContactSheet
            ? await RecognizeContactSheetAsync(frame, requestedRegions, diagnostics, cancellationToken).ConfigureAwait(false)
            : await RecognizePerRegionAsync(frame, requestedRegions, diagnostics, cancellationToken).ConfigureAwait(false);

        var dimensions = await GetRegionDimensionsAsync(frame, requestedRegions, cancellationToken).ConfigureAwait(false);
        var phaseLines = regionTexts.FirstOrDefault(item => item.Region == SmartBpRecognitionRegion.PhaseTop)?.Lines ?? [];
        var lifecycleLines = regionTexts.FirstOrDefault(item => item.Region == SmartBpRecognitionRegion.TopCenterStatus)?.Lines ?? [];
        var statusLines = regionTexts.FirstOrDefault(item => item.Region == SmartBpRecognitionRegion.TopLeftStatus)?.Lines ?? [];
        var phaseWidth = (double)dimensions.GetValueOrDefault(SmartBpRecognitionRegion.PhaseTop).Width;
        if (phaseWidth <= 0)
            phaseWidth = phaseLines.Select(line => line.CenterX).DefaultIfEmpty(1).Max() * 2;
        var postBpStatus = SmartBpPostBpStatusDetector.Detect(statusLines);
        SmartBpLifecycleStatusResult? lifecycleStatus = null;
        if (requestedRegions.Contains(SmartBpRecognitionRegion.TopCenterStatus))
        {
            lifecycleStatus = lifecycleDetector.Detect(lifecycleLines);
            foreach (var diagnostic in lifecycleStatus.Diagnostics)
                diagnostics.Add(diagnostic);
        }
        diagnostics.Add($"TopLeftStatus OCR raw text={postBpStatus.Evidence}");
        diagnostics.Add($"TopLeftStatus OCR normalized={postBpStatus.NormalizedText}");
        diagnostics.Add($"TopLeftStatus OCR matched_title={(postBpStatus.IsPostBp ? postBpStatus.Phase : "none")}; score={postBpStatus.Score:0.00}; auxiliary=[{string.Join(", ", postBpStatus.AuxiliaryEvidence)}]");
        SmartBpPhaseRecognitionResult phase;
        if (postBpStatus.IsPostBp)
        {
            phase = new SmartBpPhaseRecognitionResult { Phase = postBpStatus.Phase };
            diagnostics.Add($"Pure OCR post-BP fuzzy anchor matched: phase={postBpStatus.Phase}; evidence=\"{postBpStatus.Evidence}\"; score={postBpStatus.Score:0.00}; reason={postBpStatus.Reason}.");
        }
        else
        {
            phase = SmartBpOcrPhaseClassifier.Classify(phaseLines, phaseWidth, diagnostics);
        }

        var parsed = new Dictionary<SmartBpRecognitionRegion, SmartBpFocusedBusinessExtractionResult>();
        var effectiveParseContext = request.ParseContext ?? new SmartBpOcrFieldParseContext { AuthoritativePhase = phase.Phase };
        foreach (var regionText in regionTexts.Where(item => item.Region is not SmartBpRecognitionRegion.PhaseTop and not SmartBpRecognitionRegion.TopCenterStatus and not SmartBpRecognitionRegion.TopLeftStatus))
        {
            var parsedRegion = parser.ParseDetailed(
                regionText.Region,
                regionText.Lines,
                effectiveParseContext,
                dimensions.GetValueOrDefault(regionText.Region).Width);
            parsed[regionText.Region] = parsedRegion.Result;
            diagnostics.AddRange(parsedRegion.Diagnostics);
            foreach (var line in regionText.Lines)
                diagnostics.Add($"provider={line.Provider ?? "unknown"}; region={ToRegionId(regionText.Region)}; coordinateSpace=region-local; text={line.Text}; bbox={line.BoundingBox}; center={line.CenterX:0.0},{line.CenterY:0.0}; confidence={line.Confidence:0.00}");
        }

        var state = merger.Merge(
            phase,
            parsed.GetValueOrDefault(SmartBpRecognitionRegion.RightTop),
            parsed.GetValueOrDefault(SmartBpRecognitionRegion.LeftTop),
            parsed.GetValueOrDefault(SmartBpRecognitionRegion.LeftBottom),
            parsed.GetValueOrDefault(SmartBpRecognitionRegion.RightBottom));
        return new()
        {
            Phase = phase,
            BusinessState = state,
            Regions = regionTexts,
            LifecycleStatus = lifecycleStatus,
            PostBpStatus = requestedRegions.Contains(SmartBpRecognitionRegion.TopLeftStatus) ? postBpStatus : null,
            Diagnostics = diagnostics
        };
    }

    private async Task<IReadOnlyList<SmartBpOcrRegionText>> RecognizeContactSheetAsync(
        BitmapSource frame,
        IReadOnlyList<SmartBpRecognitionRegion> requestedRegions,
        ICollection<string> diagnostics,
        CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var sheet = contactSheetBuilder.Build(frame, requestedRegions);
            var result = ocr.RecognizeTextLines(sheet.Image);
            var grouped = SmartBpOcrContactSheetMapper.MapLinesToRegions(result, sheet.Regions, out var unmapped);
            diagnostics.Add($"provider={result.Provider ?? ocr.SelectedProvider.ToString()}; line_count={result.Lines.Count}; OCR contact sheet regions=[{string.Join(", ", requestedRegions.Select(ToRegionId))}], unmapped={unmapped}.");
            foreach (var region in sheet.Regions.Where(region => region.Region is SmartBpRecognitionRegion.TopCenterStatus or SmartBpRecognitionRegion.TopLeftStatus))
                AddStatusCropDiagnostics(diagnostics, cropper.CropWithInfo(frame, region.Region));
            return grouped;
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<SmartBpOcrRegionText>> RecognizePerRegionAsync(
        BitmapSource frame,
        IReadOnlyList<SmartBpRecognitionRegion> requestedRegions,
        ICollection<string> diagnostics,
        CancellationToken cancellationToken)
    {
        var groups = new List<SmartBpOcrRegionText>();
        foreach (var region in requestedRegions)
        {
            var lines = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var crop = cropper.CropWithInfo(frame, region);
                using var raw = BitmapSourceConverter.ToMat(crop.Image);
                using var bgr = ToBgr(raw);
                var result = ocr.RecognizeTextLines(bgr).Lines;
                if (region is SmartBpRecognitionRegion.TopCenterStatus or SmartBpRecognitionRegion.TopLeftStatus)
                    AddStatusCropDiagnostics(diagnostics, crop);
                return result;
            }, cancellationToken).ConfigureAwait(false);
            diagnostics.Add($"provider={ocr.SelectedProvider}; line_count={lines.Count}; OCR per-region fallback={ToRegionId(region)}.");
            groups.Add(new() { Region = region, Lines = lines });
        }

        return groups;
    }

    private async Task<IReadOnlyDictionary<SmartBpRecognitionRegion, Size>> GetRegionDimensionsAsync(
        BitmapSource frame,
        IReadOnlyList<SmartBpRecognitionRegion> requestedRegions,
        CancellationToken cancellationToken) =>
        await Task.Run(() => requestedRegions
            .Distinct()
            .Select(region => cropper.CropWithInfo(frame, region))
            .ToDictionary(crop => crop.Region, crop => new Size(crop.Width, crop.Height)), cancellationToken).ConfigureAwait(false);

    private static IReadOnlyList<SmartBpRecognitionRegion> BuildRequestedRegions(SmartBpOcrRecognitionRequest request)
    {
        var regions = new List<SmartBpRecognitionRegion>();
        if (request.IncludePhase)
        {
            regions.Add(SmartBpRecognitionRegion.PhaseTop);
            regions.Add(SmartBpRecognitionRegion.TopLeftStatus);
        }
        regions.AddRange(request.ContentRegions.Where(region => region != SmartBpRecognitionRegion.PhaseTop));
        return regions.Distinct().ToArray();
    }

    private static Mat ToBgr(Mat source)
    {
        var result = new Mat();
        if (source.Channels() == 1)
            Cv2.CvtColor(source, result, ColorConversionCodes.GRAY2BGR);
        else if (source.Channels() == 4)
            Cv2.CvtColor(source, result, ColorConversionCodes.BGRA2BGR);
        else
            source.CopyTo(result);
        return result;
    }

    internal static string ToRegionId(SmartBpRecognitionRegion region) => region switch
    {
        SmartBpRecognitionRegion.PhaseTop => "phase_top",
        SmartBpRecognitionRegion.TopCenterStatus => "top_center_status",
        SmartBpRecognitionRegion.TopLeftStatus => "top_left_status",
        SmartBpRecognitionRegion.LeftTop => "left_top",
        SmartBpRecognitionRegion.RightTop => "right_top",
        SmartBpRecognitionRegion.LeftBottom => "left_bottom",
        SmartBpRecognitionRegion.RightBottom => "right_bottom",
        _ => region.ToString()
    };

    private static void AddStatusCropDiagnostics(ICollection<string> diagnostics, SmartBpCroppedFrame crop)
    {
        var id = ToRegionId(crop.Region);
        diagnostics.Add($"{id} crop source={crop.LayoutSource}");
        diagnostics.Add($"{id} normalized rect={crop.NormalizedRectText}");
        diagnostics.Add($"{id} pixel rect={crop.PixelRectText}");
    }
}

/// <summary>
/// 将纯 OCR 业务状态识别结果转换为自动识别状态机使用的快照增量。
/// </summary>
