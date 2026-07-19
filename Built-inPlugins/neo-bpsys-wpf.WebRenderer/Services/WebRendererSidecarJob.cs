using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace neo_bpsys_wpf.WebRenderer.Services;

/// <summary>
/// 将 sidecar 绑定到宿主进程生命周期的 Windows Job Object。
/// </summary>
/// <remarks>
/// Job 句柄由宿主持有。宿主正常退出、崩溃或被终止时，Windows 会关闭
/// 该句柄并终止其中的全部 sidecar 进程，不依赖 WPF 的异步退出回调。
/// </remarks>
internal sealed class WebRendererSidecarJob : IDisposable
{
    private const uint JobObjectLimitKillOnJobClose = 0x00002000;
    private const int JobObjectExtendedLimitInformationClass = 9;
    private readonly SafeJobHandle _handle;

    /// <summary>创建用于 sidecar 的 Job Object。</summary>
    /// <exception cref="Win32Exception">无法创建或配置 Job Object 时引发。</exception>
    public WebRendererSidecarJob()
    {
        _handle = NativeMethods.CreateJobObject(IntPtr.Zero, null);
        if (_handle.IsInvalid)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to create Web Renderer sidecar job.");

        var limits = new JobObjectExtendedLimitInformation
        {
            BasicLimitInformation = new JobObjectBasicLimitInformation
            {
                LimitFlags = JobObjectLimitKillOnJobClose
            }
        };
        if (!NativeMethods.SetInformationJobObject(
                _handle,
                JobObjectExtendedLimitInformationClass,
                ref limits,
                (uint)Marshal.SizeOf<JobObjectExtendedLimitInformation>()))
        {
            var error = Marshal.GetLastWin32Error();
            _handle.Dispose();
            throw new Win32Exception(error, "Unable to configure Web Renderer sidecar job.");
        }
    }

    /// <summary>将已启动的 sidecar 加入 Job。</summary>
    /// <param name="process">sidecar 进程。</param>
    /// <exception cref="Win32Exception">无法分配进程时引发。</exception>
    public void Assign(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);
        if (!NativeMethods.AssignProcessToJobObject(_handle, process.Handle))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to assign Web Renderer sidecar to its job.");
    }

    /// <inheritdoc />
    public void Dispose() => _handle.Dispose();

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectBasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectExtendedLimitInformation
    {
        public JobObjectBasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    private sealed class SafeJobHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeJobHandle() : base(true) { }
        protected override bool ReleaseHandle() => NativeMethods.CloseHandle(handle);
    }

    private static class NativeMethods
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern SafeJobHandle CreateJobObject(IntPtr jobAttributes, string? name);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetInformationJobObject(SafeJobHandle job, int informationClass, ref JobObjectExtendedLimitInformation information, uint informationLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AssignProcessToJobObject(SafeJobHandle job, IntPtr process);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr handle);
    }
}
