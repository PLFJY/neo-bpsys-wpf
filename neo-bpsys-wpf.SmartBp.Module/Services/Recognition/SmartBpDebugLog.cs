using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

namespace neo_bpsys_wpf.SmartBp.Module.Services.Recognition;

internal sealed class SmartBpDebugLog : ISmartBpDebugLog
{
    public event EventHandler<SmartBpDebugMessageEventArgs>? MessageWritten;

    public bool IsEnabled { get; set; } = true;

    public void Write(string source, string message)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(message)) return;
        MessageWritten?.Invoke(this, new SmartBpDebugMessageEventArgs(DateTimeOffset.Now, source, message));
    }
}
