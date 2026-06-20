using System.ComponentModel;

namespace neo_bpsys_wpf.Models.Plugins;

/// <summary>
/// 表示插件市场和应用更新共用的下载镜像选项。
/// </summary>
public class PluginMarketMirrorOption : INotifyPropertyChanged
{
    private int? _latencyMs;

    /// <summary>
    /// 下拉框中显示的本地化 Key 或直接显示文本。
    /// </summary>
    public string DisplayNameKey { get; init; } = string.Empty;

    /// <summary>
    /// 镜像实际对应的地址值。
    /// </summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>
    /// 镜像延迟（毫秒）。<c>null</c> 表示尚未测试或测试失败。
    /// </summary>
    public int? LatencyMs
    {
        get => _latencyMs;
        set
        {
            if (_latencyMs == value) return;
            _latencyMs = value;
            OnPropertyChanged(nameof(LatencyMs));
            OnPropertyChanged(nameof(LatencyDisplayText));
        }
    }

    /// <summary>
    /// 延迟的显示文本，用于 ComboBoxItem 上的展示。
    /// </summary>
    public string LatencyDisplayText => LatencyMs.HasValue ? $"{LatencyMs.Value}ms" : "-";

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 引发 <see cref="PropertyChanged"/> 事件。
    /// </summary>
    /// <param name="propertyName">属性名</param>
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
