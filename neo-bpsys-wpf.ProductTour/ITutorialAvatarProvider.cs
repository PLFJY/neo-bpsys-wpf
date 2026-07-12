using System.Windows.Media;

namespace neo_bpsys_wpf.ProductTour;

/// <summary>
/// 描述教程引导头像所请求的姿态。
/// </summary>
public enum TutorialAvatarPose
{
    /// <summary>中性待机姿态。</summary>
    Idle,

    /// <summary>指向左上方向的姿态。</summary>
    LeftTop,

    /// <summary>指向左下方向的姿态。</summary>
    LeftBottom,

    /// <summary>指向右上方向的姿态。</summary>
    RightTop,

    /// <summary>指向右下方向的姿态。</summary>
    RightBottom
}

/// <summary>
/// 表示一个教程引导头像图像及其本地化显示名称。
/// </summary>
public sealed class TutorialAvatar
{
    /// <summary>获取头像的本地化显示名称。</summary>
    public required string DisplayName { get; init; }

    /// <summary>获取头像图像源。</summary>
    public required ImageSource ImageSource { get; init; }
}

/// <summary>
/// 为遮罩 UI 提供教程引导头像。
/// </summary>
public interface ITutorialAvatarProvider
{
    /// <summary>
    /// 获取所请求姿态对应的头像。
    /// </summary>
    /// <param name="pose">所请求的头像姿态。</param>
    /// <returns>头像；若没有可用头像则返回 <see langword="null" />。</returns>
    TutorialAvatar? GetAvatar(TutorialAvatarPose pose);
}

/// <summary>
/// 空的教程头像提供器，在宿主应用未提供引导素材时使用。
/// </summary>
public sealed class NoOpTutorialAvatarProvider : ITutorialAvatarProvider
{
    /// <inheritdoc />
    public TutorialAvatar? GetAvatar(TutorialAvatarPose pose) => null;
}
