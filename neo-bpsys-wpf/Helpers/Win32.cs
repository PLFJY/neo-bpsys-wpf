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

[Flags]
public enum PRINT_WINDOW_FLAGS : uint
{
    PW_CLIENTONLY = 0x00000001
}

[UnmanagedFunctionPointer(CallingConvention.Winapi)]
public delegate bool WNDENUMPROC(HWND hwnd, LPARAM lParam);

public static partial class Win32
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumWindows(WNDENUMPROC lpEnumFunc, LPARAM lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]

    public static extern bool IsWindowVisible(HWND hWnd);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern unsafe int GetWindowText(HWND hWnd, char* lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetWindowTextLength(HWND hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern unsafe uint GetWindowThreadProcessId(HWND hWnd, uint* lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(HWND hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern HDC GetWindowDC(HWND hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern HDC GetDC(HWND hWnd);

    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern HDC CreateCompatibleDC(HDC hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern HBITMAP CreateCompatibleBitmap(HDC hdc, int cx, int cy);

    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern HGDIOBJ SelectObject(HDC hdc, HGDIOBJ h);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteObject(HGDIOBJ ho);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PrintWindow(HWND hwnd, HDC hdcBlt, PRINT_WINDOW_FLAGS nFlags);

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

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteDC(HDC hdc);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int ReleaseDC(HWND hWnd, HDC hDC);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetClientRect(HWND hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ClientToScreen(HWND hWnd, ref System.Drawing.Point lpPoint);
}
