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

internal sealed class SmartBpOcrContactSheetBuilder(ISmartBpRecognitionFrameCropper cropper) : ISmartBpOcrContactSheetBuilder
{
    private const int Padding = 24;

    /// <summary>
    /// 构建 OCR 拼接图，并记录每个原始区域在拼接图中的位置映射。
    /// </summary>
    /// <param name="frame">完整捕获帧。</param>
    /// <param name="regions">需要识别的区域集合。</param>
    /// <returns>拼接图和区域映射信息。</returns>
    public SmartBpOcrContactSheet Build(BitmapSource frame, IReadOnlyList<SmartBpRecognitionRegion> regions)
    {
        var distinctRegions = regions.Distinct().ToArray();
        if (distinctRegions.Length == 0)
            return new(new Mat(new Size(1, 1), MatType.CV_8UC3, Scalar.All(255)), []);

        var crops = new List<(SmartBpRecognitionRegion Region, SmartBpCroppedFrame Crop, Mat Image)>();
        try
        {
            foreach (var region in distinctRegions)
            {
                var crop = cropper.CropWithInfo(frame, region);
                using var raw = BitmapSourceConverter.ToMat(crop.Image);
                crops.Add((region, crop, ToBgr(raw)));
            }

            // 合并识别可以减少 OCR Provider调用次数；映射表负责把结果再还原到区域局部坐标。
            var width = crops.Max(item => item.Image.Width);
            var height = crops.Sum(item => item.Image.Height) + Padding * Math.Max(0, crops.Count - 1);
            var sheet = new Mat(new Size(width, height), MatType.CV_8UC3, Scalar.All(255));
            var mappings = new List<SmartBpOcrContactSheetRegion>(crops.Count);
            var y = 0;
            foreach (var (region, crop, image) in crops)
            {
                using var target = new Mat(sheet, new Rect(0, y, image.Width, image.Height));
                image.CopyTo(target);
                mappings.Add(new(
                    region,
                    new Rect(0, y, image.Width, image.Height),
                    new Rect(crop.X, crop.Y, crop.Width, crop.Height)));
                y += image.Height + Padding;
            }

            return new(sheet, mappings);
        }
        finally
        {
            foreach (var item in crops)
                item.Image.Dispose();
        }
    }

    /// <summary>
    /// 把 OpenCV 图像规范化为 BGR 三通道，方便后续拼接和 OCR Provider处理。
    /// </summary>
    /// <param name="source">源图像。</param>
    /// <returns>BGR 三通道图像，调用方负责释放。</returns>
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
}

/// <summary>
/// 将拼接图上的 OCR 文本行映射回各 BP 识别区域。
/// </summary>
