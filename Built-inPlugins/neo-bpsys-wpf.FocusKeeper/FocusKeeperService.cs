using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.FocusKeeper;

/// <summary>
/// 通过 SetWindowsHookEx(WH_CBT) 注入 FocusKeeperHook.dll 到目标游戏进程，
/// 拦截窗口焦点丢失消息以防止游戏在后台暂停。
/// </summary>
public sealed class FocusKeeperService : IFocusKeeperService
{
    // ----- 第五人格常见进程名 / 窗口标题 -----
    private static readonly string[] IdentityVProcessNames =
        { "IdentityV", "Identity V" };
    private static readonly string[] IdentityVWindowTitles =
        { "第五人格", "Identity V" };

    // ----- 状态字段 -----
    private readonly object _lock = new();
    private readonly string _pluginDirectory;
    private readonly IElevationService _elevationService;

    private IntPtr _dllHandle;
    private IntPtr _hookHandle;
    private uint _targetProcessId;
    private uint _targetThreadId;
    private string? _targetProcessName;
    private bool _isEnabled;
    private bool _isInstalled;
    private string? _errorMessage;

    // ----- Native 委托 -----
    private InstallHookDelegate? _installHook;
    private UninstallHookDelegate? _uninstallHook;
    private CleanupSubclassesDelegate? _cleanupSubclasses;
    private SetEnabledDelegate? _setEnabled;
    private IsEnabledDelegate? _isEnabledFn;

    // ----- P/Invoke: user32 -----
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetParent(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindWindowW(string? lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessageW(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    private const uint WM_NULL = 0x0000;

    // ----- Native 导出委托类型 -----
    private delegate IntPtr InstallHookDelegate(uint targetThreadId);
    private delegate bool UninstallHookDelegate(IntPtr hookHandle);
    private delegate void CleanupSubclassesDelegate(uint targetProcessId);
    private delegate void SetEnabledDelegate([MarshalAs(UnmanagedType.Bool)] bool enabled);
    private delegate bool IsEnabledDelegate();

    /// <summary>
    /// 创建 FocusKeeperService 实例。
    /// </summary>
    /// <param name="pluginDirectory">插件运行时目录（包含 FocusKeeperHook.dll）。</param>
    /// <param name="elevationService">进程权限检测服务。</param>
    public FocusKeeperService(string pluginDirectory, IElevationService elevationService)
    {
        _pluginDirectory = pluginDirectory;
        _elevationService = elevationService;
    }

    /// <inheritdoc />
    public bool IsCurrentProcessElevated => _elevationService.IsCurrentProcessElevated;

    /// <inheritdoc />
    public bool RestartAsAdmin()
    {
        bool ok = _elevationService.RestartAsAdmin();
        if (!ok)
        {
            ErrorMessage = "已取消管理员权限重启（UAC 被拒绝）。";
        }
        return ok;
    }

    /// <inheritdoc />
    public bool IsInstalled
    {
        get { lock (_lock) return _isInstalled; }
    }

    /// <inheritdoc />
    public bool IsEnabled
    {
        get { lock (_lock) return _isEnabled; }
        set
        {
            lock (_lock)
            {
                if (_isEnabled == value) return;
                if (!_isInstalled)
                {
                    _errorMessage = "尚未注入目标进程，无法切换启用状态。";
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(ErrorMessage));
                    return;
                }
                _setEnabled?.Invoke(value);
                _isEnabled = value;
            }
            RaisePropertyChanged();
        }
    }

    /// <inheritdoc />
    public string? TargetProcessName
    {
        get { lock (_lock) return _targetProcessName; }
    }

    /// <inheritdoc />
    public int? TargetProcessId
    {
        get { lock (_lock) return _targetProcessId == 0 ? null : (int)_targetProcessId; }
    }

    /// <inheritdoc />
    public string? ErrorMessage
    {
        get { lock (_lock) return _errorMessage; }
        private set
        {
            lock (_lock) _errorMessage = value;
            RaisePropertyChanged();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<GameWindowInfo> EnumerateGameWindows()
    {
        var results = new List<GameWindowInfo>();
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd)) return true;
            if (GetParent(hwnd) != IntPtr.Zero) return true; // 只收顶层窗口

            var sb = new StringBuilder(256);
            GetWindowText(hwnd, sb, sb.Capacity);
            string title = sb.ToString();
            if (string.IsNullOrWhiteSpace(title)) return true;

            uint threadId = GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0 || threadId == 0) return true;

            string procName = SafeGetProcessName(pid);
            bool likely = IsLikelyIdentityV(procName, title);

            results.Add(new GameWindowInfo
            {
                Handle = hwnd,
                Title = title,
                ProcessName = procName,
                ProcessId = (int)pid,
                IsLikelyIdentityV = likely
            });
            return true;
        }, IntPtr.Zero);

        // 把可能为第五人格的窗口排前面
        results.Sort((a, b) => b.IsLikelyIdentityV.CompareTo(a.IsLikelyIdentityV));
        return results;
    }

    /// <inheritdoc />
    public bool FindAndInstall()
    {
        var windows = EnumerateGameWindows();
        var target = windows.FirstOrDefault(w => w.IsLikelyIdentityV)
                     ?? windows.FirstOrDefault();
        if (target is null)
        {
            ErrorMessage = "未找到任何可见的顶层窗口。请先启动目标游戏。";
            return false;
        }
        return Install(target.Handle);
    }

    /// <inheritdoc />
    public bool Install(IntPtr windowHandle)
    {
        lock (_lock)
        {
            if (_isInstalled)
            {
                ErrorMessage = "已注入到一个进程，请先卸载后再注入新目标。";
                return false;
            }

            try
            {
                EnsureNativeLoaded();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"加载 FocusKeeperHook.dll 失败：{ex.Message}";
                return false;
            }

            uint threadId = GetWindowThreadProcessId(windowHandle, out uint pid);
            if (threadId == 0 || pid == 0)
            {
                ErrorMessage = "无法获取目标窗口的线程 ID（窗口可能已关闭）。";
                return false;
            }

            IntPtr hook = _installHook!.Invoke(threadId);
            if (hook == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                // ERROR_ACCESS_DENIED (5)：通常是主程序未提权而目标游戏以管理员权限运行
                if (err == 5 && !IsCurrentProcessElevated)
                {
                    ErrorMessage = "注入被拒绝（权限不足）。目标游戏可能以管理员权限运行，请关闭主程序后以管理员身份重新启动，再尝试注入。";
                }
                else
                {
                    ErrorMessage = $"SetWindowsHookEx 失败（Win32 错误 {err}）。目标进程可能受保护。";
                }
                return false;
            }

            _hookHandle = hook;
            _targetProcessId = pid;
            _targetThreadId = threadId;
            _targetProcessName = SafeGetProcessName(pid);
            _isInstalled = true;

            // 默认启用
            _setEnabled!.Invoke(true);
            _isEnabled = true;

            // 发送 WM_NULL 触发目标线程的消息处理，使 WH_CBT 钩子立即激活，
            // 从而让懒初始化（EnumWindows + SetWindowSubclass）尽快执行。
            SendMessageW(windowHandle, WM_NULL, IntPtr.Zero, IntPtr.Zero);

            ErrorMessage = null;
        }

        RaisePropertyChanged(nameof(IsInstalled));
        RaisePropertyChanged(nameof(IsEnabled));
        RaisePropertyChanged(nameof(TargetProcessName));
        RaisePropertyChanged(nameof(TargetProcessId));
        RaisePropertyChanged(nameof(ErrorMessage));
        return true;
    }

    /// <inheritdoc />
    public void Uninstall()
    {
        lock (_lock)
        {
            if (!_isInstalled) return;

            // 1) 先清理目标进程中的所有 subclass（通过 SendMessage 跨进程触发）
            _cleanupSubclasses?.Invoke(_targetProcessId);

            // 2) 再卸载 WH_CBT 钩子（这会减少目标进程中 DLL 的引用计数）
            if (_hookHandle != IntPtr.Zero)
            {
                _uninstallHook?.Invoke(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }

            _isInstalled = false;
            _isEnabled = false;
            _targetProcessId = 0;
            _targetThreadId = 0;
            _targetProcessName = null;
        }

        RaisePropertyChanged(nameof(IsInstalled));
        RaisePropertyChanged(nameof(IsEnabled));
        RaisePropertyChanged(nameof(TargetProcessName));
        RaisePropertyChanged(nameof(TargetProcessId));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Uninstall();

        lock (_lock)
        {
            if (_dllHandle != IntPtr.Zero)
            {
                NativeLibrary.Free(_dllHandle);
                _dllHandle = IntPtr.Zero;
                _installHook = null;
                _uninstallHook = null;
                _cleanupSubclasses = null;
                _setEnabled = null;
                _isEnabledFn = null;
            }
        }
    }

    // ===================== Private Helpers =====================

    private void EnsureNativeLoaded()
    {
        if (_dllHandle != IntPtr.Zero) return;

        string dllPath = Path.Combine(_pluginDirectory, "FocusKeeperHook.dll");
        if (!File.Exists(dllPath))
            throw new FileNotFoundException(
                "FocusKeeperHook.dll 不在插件目录中。请确保 C++ 项目已成功构建。", dllPath);

        _dllHandle = NativeLibrary.Load(dllPath);
        if (_dllHandle == IntPtr.Zero)
            throw new DllNotFoundException($"无法加载 {dllPath}（可能架构不匹配）。");

        _installHook = GetDelegate<InstallHookDelegate>("FocusKeeper_InstallHook");
        _uninstallHook = GetDelegate<UninstallHookDelegate>("FocusKeeper_UninstallHook");
        _cleanupSubclasses = GetDelegate<CleanupSubclassesDelegate>("FocusKeeper_CleanupSubclasses");
        _setEnabled = GetDelegate<SetEnabledDelegate>("FocusKeeper_SetEnabled");
        _isEnabledFn = GetDelegate<IsEnabledDelegate>("FocusKeeper_IsEnabled");
    }

    private T GetDelegate<T>(string exportName) where T : Delegate
    {
        IntPtr proc = NativeLibrary.GetExport(_dllHandle, exportName);
        if (proc == IntPtr.Zero)
            throw new EntryPointNotFoundException(
                $"FocusKeeperHook.dll 中找不到导出函数 '{exportName}'。");
        return Marshal.GetDelegateForFunctionPointer<T>(proc);
    }

    private static string SafeGetProcessName(uint pid)
    {
        try
        {
            return Process.GetProcessById((int)pid).ProcessName;
        }
        catch
        {
            return "<unknown>";
        }
    }

    private static bool IsLikelyIdentityV(string processName, string windowTitle)
    {
        foreach (var name in IdentityVProcessNames)
            if (processName.Contains(name, StringComparison.OrdinalIgnoreCase))
                return true;
        foreach (var title in IdentityVWindowTitles)
            if (windowTitle.Contains(title, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    // ===================== INotifyPropertyChanged =====================

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    private void RaisePropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
