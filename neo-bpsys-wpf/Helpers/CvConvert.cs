using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace neo_bpsys_wpf.Helpers;

public static class CvConvert
{
    public static Mat ToBgrMat(this BitmapSource src)
    {
        var mat = src.ToMat(); // 常见是 BGRA
        if (mat.Channels() != 4) return mat;
        var bgr = new Mat();
        Cv2.CvtColor(mat, bgr, ColorConversionCodes.BGRA2BGR);
        mat.Dispose();
        return bgr;
    }
}