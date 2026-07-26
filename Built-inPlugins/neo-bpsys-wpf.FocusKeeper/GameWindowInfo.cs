namespace neo_bpsys_wpf.FocusKeeper;

/// <summary>
/// 描述一个可注入的游戏窗口。
/// </summary>
public sealed record GameWindowInfo
{
    /// <summary>窗口句柄。</summary>
    public IntPtr Handle { get; init; }

    /// <summary>窗口标题。</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>进程名称（不含扩展名）。</summary>
    public string ProcessName { get; init; } = string.Empty;

    /// <summary>进程 ID。</summary>
    public int ProcessId { get; init; }

    /// <summary>是否匹配第五人格的常见进程名 / 窗口标题。</summary>
    public bool IsLikelyIdentityV { get; init; }

    /// <summary>返回 "进程名 (PID) — 窗口标题" 格式的显示文本。</summary>
    public override string ToString() => $"{ProcessName} ({ProcessId}) — {Title}";
}
