using System.Runtime.InteropServices;
using System.Text;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

namespace neo_bpsys_wpf.SmartBp.Module.Services.Recognition;

internal sealed class NvmlAiPerformanceMonitor : ISmartBpAiPerformanceMonitor
{
    public Task<SmartBpAiPerformanceSnapshot> GetSnapshotAsync(int? processId, CancellationToken cancellationToken = default) =>
        Task.Run(() => Read(processId, cancellationToken), cancellationToken);

    private static SmartBpAiPerformanceSnapshot Read(int? processId, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!NativeLibrary.TryLoad("nvml.dll", out var library)) return Unavailable(processId);
        try
        {
            var init = Get<NvmlInit>(library, "nvmlInit_v2");
            var shutdown = Get<NvmlShutdown>(library, "nvmlShutdown");
            var getHandle = Get<NvmlDeviceGetHandle>(library, "nvmlDeviceGetHandleByIndex_v2");
            var getName = Get<NvmlDeviceGetName>(library, "nvmlDeviceGetName");
            var getUtilization = Get<NvmlDeviceGetUtilization>(library, "nvmlDeviceGetUtilizationRates");
            var getMemory = Get<NvmlDeviceGetMemory>(library, "nvmlDeviceGetMemoryInfo");
            if (init() != 0 || getHandle(0, out var device) != 0) return Unavailable(processId);
            try
            {
                var buffer = Marshal.AllocHGlobal(128);
                string name;
                try
                {
                    name = getName(device, buffer, 128) == 0 ? Marshal.PtrToStringAnsi(buffer) ?? "NVIDIA GPU" : "NVIDIA GPU";
                }
                finally { Marshal.FreeHGlobal(buffer); }
                var utilization = getUtilization(device, out var rates) == 0 ? rates.Gpu : (uint?)null;
                var memoryOk = getMemory(device, out var memory) == 0;
                return new(name, utilization, memoryOk ? memory.Used : null, memoryOk ? memory.Total : null,
                    processId, DateTimeOffset.Now, true);
            }
            finally { shutdown(); }
        }
        catch { return Unavailable(processId); }
        finally { NativeLibrary.Free(library); }
    }

    private static T Get<T>(nint library, string name) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library, name));
    private static SmartBpAiPerformanceSnapshot Unavailable(int? processId) =>
        new("not available", null, null, null, processId, DateTimeOffset.Now, false);

    [StructLayout(LayoutKind.Sequential)] private struct Utilization { public uint Gpu; public uint Memory; }
    [StructLayout(LayoutKind.Sequential)] private struct MemoryInfo { public ulong Total; public ulong Free; public ulong Used; }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int NvmlInit();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int NvmlShutdown();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int NvmlDeviceGetHandle(uint index, out nint device);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int NvmlDeviceGetName(nint device, nint name, uint length);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int NvmlDeviceGetUtilization(nint device, out Utilization utilization);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int NvmlDeviceGetMemory(nint device, out MemoryInfo memory);
}
