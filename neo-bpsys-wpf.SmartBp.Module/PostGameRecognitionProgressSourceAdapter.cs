using neo_bpsys_wpf.Core.Models.SmartBpModule;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using PostGameRecognitionProgress = neo_bpsys_wpf.Services.PostGameRecognitionProgress;
using PostGameRecognitionProgressEventArgs = neo_bpsys_wpf.Services.PostGameRecognitionProgressEventArgs;

namespace neo_bpsys_wpf.SmartBp.Module;

/// <summary>
/// 将模块内部的 <see cref="IPostGameRecognitionProgressSource"/> 适配为宿主侧
/// <see cref="ISmartBpPostGameRecognitionProgressSource"/>，避免模块私有类型泄漏到宿主。
/// </summary>
internal sealed class PostGameRecognitionProgressSourceAdapter : ISmartBpPostGameRecognitionProgressSource
{
    private readonly IPostGameRecognitionProgressSource _inner;

    private PostGameRecognitionProgressSourceAdapter(IPostGameRecognitionProgressSource inner)
    {
        _inner = inner;
        _inner.ProgressChanged += OnInnerProgressChanged;
    }

    /// <summary>
    /// 创建适配器；若 <paramref name="inner"/> 为 null 则返回 null。
    /// </summary>
    /// <param name="inner">模块内部进度源。</param>
    /// <returns>适配器实例，或 null。</returns>
    public static PostGameRecognitionProgressSourceAdapter? Create(IPostGameRecognitionProgressSource? inner)
        => inner is null ? null : new PostGameRecognitionProgressSourceAdapter(inner);

    /// <inheritdoc />
    public event EventHandler<SmartBpPostGameRecognitionProgressEventArgs>? ProgressChanged;

    /// <inheritdoc />
    public SmartBpPostGameRecognitionProgress CurrentProgress => ToHost(_inner.CurrentProgress);

    private void OnInnerProgressChanged(object? sender, PostGameRecognitionProgressEventArgs e)
    {
        var host = ToHost(e.Progress);
        ProgressChanged?.Invoke(sender, new SmartBpPostGameRecognitionProgressEventArgs(host));
    }

    private static SmartBpPostGameRecognitionProgress ToHost(PostGameRecognitionProgress progress)
        => new(progress.Percent, progress.StageText);
}
