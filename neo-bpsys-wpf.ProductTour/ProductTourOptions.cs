using System.Windows;

namespace neo_bpsys_wpf.ProductTour;

/// <summary>
/// 配置 Product Tour 的布局、动画和显示行为。
/// </summary>
public sealed class ProductTourOptions
{
    /// <summary>获取或设置产品导览卡片宽度。</summary>
    public double CardWidth { get; set; } = 380;

    /// <summary>获取或设置产品导览卡片最大高度。</summary>
    public double CardMaxHeight { get; set; } = 280;

    /// <summary>获取或设置卡片相对宿主边界的最小边距。</summary>
    public double CardMargin { get; set; } = 12;

    /// <summary>获取或设置目标与卡片之间的间距。</summary>
    public double Gap { get; set; } = 16;

    /// <summary>获取或设置目标周围的聚光灯内边距。</summary>
    public double SpotlightPadding { get; set; } = 8;

    /// <summary>获取或设置聚光灯圆角半径。</summary>
    public double SpotlightCornerRadius { get; set; } = 8;

    /// <summary>获取或设置遮罩淡入持续时间。</summary>
    public TimeSpan OverlayFadeInDuration { get; set; } = TimeSpan.FromMilliseconds(240);

    /// <summary>获取或设置遮罩淡出持续时间。</summary>
    public TimeSpan OverlayFadeOutDuration { get; set; } = TimeSpan.FromMilliseconds(220);

    /// <summary>获取或设置欢迎遮罩淡入持续时间。</summary>
    public TimeSpan WelcomeFadeInDuration { get; set; } = TimeSpan.FromMilliseconds(240);

    /// <summary>获取或设置欢迎遮罩淡出持续时间。</summary>
    public TimeSpan WelcomeFadeOutDuration { get; set; } = TimeSpan.FromMilliseconds(280);

    /// <summary>获取或设置欢迎卡片进入动画持续时间。</summary>
    public TimeSpan WelcomeCardEnterDuration { get; set; } = TimeSpan.FromMilliseconds(300);

    /// <summary>获取或设置欢迎卡片初始垂直位移。</summary>
    public double WelcomeCardInitialTranslateY { get; set; } = 16;

    /// <summary>获取或设置对话遮罩淡入持续时间。</summary>
    public TimeSpan DialogueFadeInDuration { get; set; } = TimeSpan.FromMilliseconds(240);

    /// <summary>获取或设置对话遮罩淡出持续时间。</summary>
    public TimeSpan DialogueFadeOutDuration { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>获取或设置对话框进入动画持续时间。</summary>
    public TimeSpan DialogueBoxEnterDuration { get; set; } = TimeSpan.FromMilliseconds(280);

    /// <summary>获取或设置对话框初始垂直位移。</summary>
    public double DialogueInitialTranslateY { get; set; } = 24;

    /// <summary>获取或设置对话打字机间隔。</summary>
    public TimeSpan TypewriterInterval { get; set; } = TimeSpan.FromMilliseconds(28);

    /// <summary>获取或设置是否显示步骤进度文本。</summary>
    public bool ShowStepProgress { get; set; } = true;

    /// <summary>获取或设置默认遮罩不透明度。</summary>
    public double MaskOpacity { get; set; } = 0.86;

    /// <summary>获取或设置欢迎遮罩不透明度。</summary>
    public double WelcomeMaskOpacity { get; set; } = 0.90;

    /// <summary>获取或设置对话遮罩不透明度。</summary>
    public double DialogueMaskOpacity { get; set; } = 0.82;

    /// <summary>获取或设置产品导览遮罩不透明度。</summary>
    public double ProductTourMaskOpacity { get; set; } = 0.84;

    /// <summary>获取或设置对话框最大宽度。</summary>
    public double DialogueBoxMaxWidth { get; set; } = 760;

    /// <summary>获取或设置对话框表面期望的最小不透明度。</summary>
    public double DialogueBoxMinOpacity { get; set; } = 0.94;

    /// <summary>获取或设置对话框边距。</summary>
    public Thickness DialogueBoxMargin { get; set; } = new(48);

    /// <summary>获取或设置在可用时是否显示引导头像。</summary>
    public bool ShowAvatar { get; set; } = true;

    /// <summary>获取或设置欢迎引导头像宽度。</summary>
    public double WelcomeAvatarWidth { get; set; } = 220;

    /// <summary>获取或设置对话引导头像宽度。</summary>
    public double DialogueAvatarWidth { get; set; } = 260;

    /// <summary>获取或设置产品导览引导头像宽度。</summary>
    public double ProductTourAvatarWidth { get; set; } = 96;

    /// <summary>获取或设置引导头像边距。</summary>
    public Thickness AvatarMargin { get; set; } = new(16);
}
