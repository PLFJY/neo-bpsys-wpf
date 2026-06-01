using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// v3 带外层容器图片控件的外层 Border 与内层 Image 尺寸关系。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ImageSizingMode
{
    /// <summary>
    /// 保留 WPF Image 的默认测量和排列行为。
    /// </summary>
    Auto,

    /// <summary>
    /// 内层 Image 填满外层容器分配的布局槽。
    /// </summary>
    FillContainer,

    /// <summary>
    /// 内层 Image 保持自身布局尺寸，由外层 Border 负责裁剪溢出内容。
    /// </summary>
    OverflowCrop
}
