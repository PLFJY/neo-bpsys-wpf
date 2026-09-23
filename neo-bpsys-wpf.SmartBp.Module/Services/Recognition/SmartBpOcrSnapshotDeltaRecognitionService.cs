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

internal sealed class SmartBpOcrSnapshotDeltaRecognitionService(
    ISmartBpOcrBpRecognitionService ocrRecognition,
    ISmartBpRecognitionFrameCropper cropper) : ISmartBpOcrSnapshotDeltaRecognitionService
{
    public async Task<(SmartBpSnapshotDeltaResult Delta, IReadOnlyDictionary<string, string> RawResponses, SmartBpCroppedFrame PhaseCrop, IReadOnlyList<SmartBpCroppedFrame> ContentCrops, IReadOnlyList<string> Diagnostics)> RecognizeDeltaAsync(
        BitmapSource frame,
        SmartBpSnapshotDeltaRequest request,
        long frameSequence,
        CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        var contentRegions = request.RequestedRegions.Select(item => item.Region).Distinct().ToArray();
        var result = await ocrRecognition.RecognizeAsync(frame, new SmartBpOcrRecognitionRequest(contentRegions), cancellationToken).ConfigureAwait(false);
        watch.Stop();

        var delta = ToDelta(result.BusinessState, request.RequestedFields);
        var phaseCrop = await Task.Run(() => cropper.CropWithInfo(frame, SmartBpRecognitionRegion.PhaseTop), cancellationToken).ConfigureAwait(false);
        var crops = new List<SmartBpCroppedFrame>();
        foreach (var region in contentRegions)
            crops.Add(await Task.Run(() => cropper.CropWithInfo(frame, region), cancellationToken).ConfigureAwait(false));

        var diagnostics = new List<string>
        {
            $"Frame sequence {frameSequence}: OCR requested fields [{string.Join(", ", request.RequestedFields)}].",
            $"OCR elapsed time {watch.ElapsedMilliseconds}ms.",
            $"OCR delta updates=[{string.Join(", ", delta.Updates.Select(update => update.Field))}]."
        };
        diagnostics.AddRange(request.Diagnostics);
        diagnostics.AddRange(result.Diagnostics);
        return (delta, new Dictionary<string, string> { ["ocr_lines"] = FormatRawLines(result) }, phaseCrop, crops, diagnostics);
    }

    private static SmartBpSnapshotDeltaResult ToDelta(
        SmartBpBusinessStateRecognitionResult state,
        IReadOnlyCollection<string> requestedFields)
    {
        var updates = new List<SmartBpSnapshotFieldUpdate>();
        if (requestedFields.Contains("banned_sur"))
            updates.Add(new() { Field = "banned_sur", Slots = state.BannedSur.Select(ToDeltaSlot).ToList() });
        if (requestedFields.Contains("banned_hun"))
            updates.Add(new() { Field = "banned_hun", Slots = state.BannedHun.Select(ToDeltaSlot).ToList() });
        if (requestedFields.Contains("picked_sur"))
            updates.Add(new() { Field = "picked_sur", Slots = state.PickedSur.Select(ToDeltaSlot).ToList() });
        if (requestedFields.Contains("picked_hun"))
            updates.Add(new() { Field = "picked_hun", PickedHun = ToDeltaSlot(state.PickedHun) });
        return new() { Phase = state.Phase, Updates = updates };
    }

    private static string FormatRawLines(SmartBpOcrRecognitionResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"phase={result.Phase.Phase}");
        foreach (var region in result.Regions)
        {
            builder.AppendLine($"[{SmartBpOcrBpRecognitionService.ToRegionId(region.Region)}]");
            foreach (var line in region.Lines)
                builder.AppendLine($"provider={line.Provider ?? "unknown"}\tcoordinateSpace=region-local\ttext={line.Text}\tbbox={line.BoundingBox}\tcenter={line.CenterX:0.0},{line.CenterY:0.0}\tconf={line.Confidence:0.00}");
        }

        return builder.ToString().TrimEnd();
    }

    private static SmartBpSnapshotDeltaSlot ToDeltaSlot(SmartBpRecognizedCharacterSlot slot) =>
        new()
        {
            Index = slot.Index,
            SlotState = slot.SlotState switch
            {
                SmartBpRecognizedSlotState.Selected => "selected",
                SmartBpRecognizedSlotState.Empty => "empty",
                _ => "unknown"
            },
            CharacterName = slot.CharacterName,
            RecognitionConfidence = slot.RecognitionConfidence,
            IsAutoApplySafe = slot.IsAutoApplySafe,
            RecognitionReason = slot.RecognitionReason,
            BoundingBox = slot.BoundingBox
        };

    private static SmartBpSnapshotDeltaSlot ToDeltaSlot(SmartBpRecognizedPlayerCharacterSlot slot) =>
        new()
        {
            Index = slot.Index,
            SlotState = slot.SlotState switch
            {
                SmartBpRecognizedSlotState.Selected => "selected",
                SmartBpRecognizedSlotState.Empty => "empty",
                _ => "unknown"
            },
            CharacterName = slot.CharacterName,
            PlayerId = slot.PlayerId,
            RecognitionConfidence = slot.RecognitionConfidence,
            IsAutoApplySafe = slot.IsAutoApplySafe,
            RecognitionReason = slot.RecognitionReason,
            BoundingBox = slot.BoundingBox
        };
}
