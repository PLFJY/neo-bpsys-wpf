using System.IO;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Helpers;
using OpenCvSharp;

namespace neo_bpsys_wpf.Services;

public class SmartBpService : ISmartBpService
{
    private readonly IWindowCaptureService _windowCaptureService;
    private readonly IOcrService _ocrService;

    public bool IsSmartBpRunning { get; private set; }

    public SmartBpService(IWindowCaptureService windowCaptureService, IOcrService ocrService)
    {
        _windowCaptureService = windowCaptureService;
        _ocrService = ocrService;
    }

    public void StartSmartBp()
    {
        if (!IsOcrReady())
        {
            IsSmartBpRunning = false;
            _ = MessageBoxHelper.ShowErrorAsync("OCR 功能未就绪，请先下载并切换 OCR 模型后再启动相关功能。");
            return;
        }

        IsSmartBpRunning = true;
    }

    public void StopSmartBp()
    {
        IsSmartBpRunning = false;
    }

    public void AutoFillGameData()
    {
    }

    private bool IsOcrReady()
    {
        var currentModelKey = _ocrService.CurrentOcrModelKey;
        return !string.IsNullOrWhiteSpace(currentModelKey) &&
               _ocrService.IsModelInstalled(currentModelKey);
    }

    public void DebugExtractGameData()
    {
        var frame = _windowCaptureService.GetCurrentFrame();
        if (frame == null)
            throw new InvalidOperationException("No frame.");

        using var mat = frame.ToBgrMat();

        // SaveDebug(mat, "01_full.png");

        // === Step 1: 裁剪 Table ===
        var tableRect = new RelativeRect(0.1, 0.165, 0.845, 0.62)
            .ToPixelRect(mat.Width, mat.Height);

        using var table = new Mat(mat, tableRect);
        // SaveDebug(table, "02_table.png");

        // === Step 2: 裁剪 Hunter 行 ===
        var hunterRect = new RelativeRect(0, 0, 1, 0.16)
            .ToPixelRect(table.Width, table.Height);
        using var hunter = new Mat(table, hunterRect);
        // SaveDebug(hunter, "03_hunter_row.png");

        // === Step 3: 裁剪 Name 列 ===
        var nameRect = new RelativeRect(0, 0, 0.4, 0.5)
            .ToPixelRect(hunter.Width, hunter.Height);

        using var name = new Mat(hunter, nameRect);
        // SaveDebug(name, "04_hunter_name.png");

        // === Step 4: 预处理 ===
        using var bin = PreprocessForText(name);
        // SaveDebug(bin, "05_hunter_name_bin.png");

        _ocrService.RecognizeText(bin);
    }

    private static void SaveDebug(Mat mat, string fileName)
    {
        var outputDir = Path.Combine(
            AppContext.BaseDirectory,
            "debug");

        Directory.CreateDirectory(outputDir);

        var fullPath = Path.Combine(outputDir, fileName);
        mat.SaveImage(fullPath);
    }

    public readonly record struct RelativeRect(double X, double Y, double W, double H)
    {
        public Rect ToPixelRect(int imgW, int imgH)
        {
            var x = (int)(X * imgW);
            var y = (int)(Y * imgH);
            var w = (int)(W * imgW);
            var h = (int)(H * imgH);
            return new Rect(x, y, w, h);
        }
    }

    public static Mat PreprocessForText(Mat bgr)
    {
        var scaled = new Mat();
        Cv2.Resize(bgr, scaled, new Size(), 2.0, 2.0, InterpolationFlags.Cubic);

        var gray = new Mat();
        Cv2.CvtColor(scaled, gray, ColorConversionCodes.BGR2GRAY);

        var blur = new Mat();
        Cv2.GaussianBlur(gray, blur, new Size(3, 3), 0);

        var bin = new Mat();
        Cv2.AdaptiveThreshold(
            blur, bin,
            255,
            AdaptiveThresholdTypes.GaussianC,
            ThresholdTypes.Binary,
            31,
            5);

        return bin;
    }
}
