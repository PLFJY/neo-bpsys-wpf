using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

namespace neo_bpsys_wpf.SmartBp.Module.Services.Recognition;

/// <summary>
/// 向 SmartBP 识别页面广播实时调试日志消息。
/// </summary>
internal sealed class SmartBpDebugLog : ISmartBpDebugLog
{
    /// <inheritdoc />
    public event EventHandler<SmartBpDebugMessageEventArgs>? MessageWritten;

    /// <inheritdoc />
    public bool IsEnabled { get; set; } = true;

    /// <inheritdoc />
    public void Write(string source, string message)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(message)) return;
        MessageWritten?.Invoke(this, new SmartBpDebugMessageEventArgs(DateTimeOffset.Now, source, message));
    }
}
