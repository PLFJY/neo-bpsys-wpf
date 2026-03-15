namespace neo_bpsys_wpf.Core.Models;

/// <summary>
/// 窗口信息
/// </summary>
/// <param name="Hwnd">窗口句柄</param>
/// <param name="Title">窗口标题</param>
/// <param name="ProcessName">进程名称</param>
/// <param name="ProcessId">进程 ID</param>
public sealed record WindowInfo(
    nint Hwnd,
    string Title,
    string ProcessName,
    uint ProcessId
)
{
    /// <inheritdoc />
    public override string ToString() => $"[{ProcessName}.exe]: {(Title.Length <= 0 ? "null" : Title)}";
}