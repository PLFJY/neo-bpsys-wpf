namespace neo_bpsys_wpf.Core.Models.SmartBpModule;

/// <summary>
/// 赛后数据识别进度的宿主侧快照。
/// </summary>
/// <param name="Percent">非线性进度百分比（0~100）。</param>
/// <param name="StageText">已本地化的阶段提示文本，供 UI 直接显示。</param>
public sealed record SmartBpPostGameRecognitionProgress(int Percent, string StageText)
{
    /// <summary>空闲状态快照。</summary>
    public static SmartBpPostGameRecognitionProgress Idle { get; } = new(0, string.Empty);
}

/// <summary>
/// 赛后数据识别进度变化事件参数。
/// </summary>
public sealed class SmartBpPostGameRecognitionProgressEventArgs : EventArgs
{
    /// <summary>获取当前进度快照。</summary>
    public SmartBpPostGameRecognitionProgress Progress { get; }

    /// <summary>初始化 <see cref="SmartBpPostGameRecognitionProgressEventArgs"/> 的新实例。</summary>
    /// <param name="progress">进度快照。</param>
    public SmartBpPostGameRecognitionProgressEventArgs(SmartBpPostGameRecognitionProgress progress)
    {
        Progress = progress;
    }
}

/// <summary>
/// 向宿主提供赛后数据识别的实时进度。
/// </summary>
public interface ISmartBpPostGameRecognitionProgressSource
{
    /// <summary>赛后数据识别进度变化时触发。</summary>
    event EventHandler<SmartBpPostGameRecognitionProgressEventArgs>? ProgressChanged;

    /// <summary>获取最近一次进度快照；未开始识别时为 <see cref="SmartBpPostGameRecognitionProgress.Idle"/>。</summary>
    SmartBpPostGameRecognitionProgress CurrentProgress { get; }
}
