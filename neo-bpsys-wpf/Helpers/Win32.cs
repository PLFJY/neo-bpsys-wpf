global using HBITMAP = nint;
global using HDC = nint;
global using HGDIOBJ = nint;
global using HWND = nint;
global using LPARAM = nint;

using System.Runtime.InteropServices;

namespace neo_bpsys_wpf.Helpers;

[StructLayout(LayoutKind.Sequential)]
public struct RECT
{
    public int left;
    public int top;
    public int right;
    public int bottom;
}

[Flags]
public enum ROP_CODE : uint
{
    SRCCOPY = 0x00CC0020,
    CAPTUREBLT = 0x40000000
}

/// <summary>
/// PrintWindow 的标志位。
/// </summary>
[Flags]
public enum PRINT_WINDOW_FLAGS : uint
{
    PW_CLIENTONLY = 0x00000001
}

[UnmanagedFunctionPointer(CallingConvention.Winapi)]
public delegate bool WNDENUMPROC(HWND hwnd, LPARAM lParam);

public static partial class Win32
{
    /// <summary>
    /// 枚举屏幕上的所有顶层窗口。
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumWindows(WNDENUMPROC lpEnumFunc, LPARAM lParam);

    /// <summary>
    /// 判断指定窗口是否可见。
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]

    public static extern bool IsWindowVisible(HWND hWnd);

    /// <summary>
    /// 获取窗口标题文本。
    /// </summary>
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern unsafe int GetWindowText(HWND hWnd, char* lpString, int nMaxCount);

    /// <summary>
    /// 获取窗口标题文本的长度。
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetWindowTextLength(HWND hWnd);

    /// <summary>
    /// 获取创建指定窗口的线程 ID 和进程 ID。
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern unsafe uint GetWindowThreadProcessId(HWND hWnd, uint* lpdwProcessId);

    /// <summary>
    /// 获取窗口的屏幕坐标矩形。
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(HWND hWnd, out RECT lpRect);

    /// <summary>
    /// 获取窗口设备上下文（包含非客户区）。
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern HDC GetWindowDC(HWND hWnd);

    /// <summary>
    /// 获取窗口客户区设备上下文。
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern HDC GetDC(HWND hWnd);

    /// <summary>
    /// 创建与指定设备上下文兼容的内存设备上下文。
    /// </summary>
    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern HDC CreateCompatibleDC(HDC hdc);

    /// <summary>
    /// 创建与指定设备上下文兼容的位图。
    /// </summary>
    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern HBITMAP CreateCompatibleBitmap(HDC hdc, int cx, int cy);

    /// <summary>
    /// 将 GDI 对象选入设备上下文。
    /// </summary>
    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern HGDIOBJ SelectObject(HDC hdc, HGDIOBJ h);

    /// <summary>
    /// 删除 GDI 对象并释放资源。
    /// </summary>
    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteObject(HGDIOBJ ho);

    /// <summary>
    /// 将窗口内容打印到指定的设备上下文。
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PrintWindow(HWND hwnd, HDC hdcBlt, PRINT_WINDOW_FLAGS nFlags);

    /// <summary>
    /// 将像素从源设备上下文传输到目标设备上下文。
    /// </summary>
    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool BitBlt(
        HDC hdc,
        int x,
        int y,
        int cx,
        int cy,
        HDC hdcSrc,
        int x1,
        int y1,
        ROP_CODE rop);

    /// <summary>
    /// 删除设备上下文。
    /// </summary>
    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteDC(HDC hdc);

    /// <summary>
    /// 释放设备上下文。
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern int ReleaseDC(HWND hWnd, HDC hDC);

    /// <summary>
    /// 获取窗口客户区的矩形坐标。
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetClientRect(HWND hWnd, out RECT lpRect);

    /// <summary>
    /// 将客户区坐标转换为屏幕坐标。
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ClientToScreen(HWND hWnd, ref System.Drawing.Point lpPoint);
}
