using System.Runtime.InteropServices;
using CUresult = System.Int32;
using CUdevice = System.Int32;

namespace neo_bpsys_wpf.SmartBp.Module.PaddleRuntime;

/// <summary>
/// 通过 <see cref="LibraryImportAttribute"/> 源生成器声明 NVIDIA CUDA Driver API（<c>nvcuda.dll</c>）的 P/Invoke 入口。
/// </summary>
/// <remarks>
/// 本类仅包含原生函数声明，不包含业务逻辑。业务调用方应通过 <see cref="CudaDeviceDetector"/> 间接使用，
/// 以便在测试时替换 <see cref="ICudaNativeMethods"/> 的实现而不触发真实 P/Invoke。
/// </remarks>
internal static partial class CudaNativeMethods
{
    /// <summary>
    /// nvcuda.dll 的库名。
    /// </summary>
    private const string NvcudaLibrary = "nvcuda.dll";

    /// <summary>
    /// CUDA Driver API 的成功返回码。所有其他非零值均表示错误。
    /// </summary>
    public const int CudaSuccess = 0;

    /// <summary>
    /// 初始化 CUDA Driver API 上下文。必须在调用任何其他 CUDA Driver API 之前调用。
    /// </summary>
    /// <param name="flags">初始化标志，当前必须为 0。</param>
    /// <returns>CUDA 错误码；<see cref="CudaSuccess"/> 表示成功。</returns>
    [LibraryImport(NvcudaLibrary, SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial CUresult cuInit(uint flags);

    /// <summary>
    /// 获取已安装的 CUDA Driver 版本号。
    /// </summary>
    /// <param name="driverVersion">输出驱动版本号（如 12020 表示 12.2）。</param>
    /// <returns>CUDA 错误码；<see cref="CudaSuccess"/> 表示成功。</returns>
    [LibraryImport(NvcudaLibrary, SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial CUresult cuDriverGetVersion(out int driverVersion);

    /// <summary>
    /// 获取系统中可用的 CUDA 设备数量。
    /// </summary>
    /// <param name="count">输出设备数量。</param>
    /// <returns>CUDA 错误码；<see cref="CudaSuccess"/> 表示成功。</returns>
    [LibraryImport(NvcudaLibrary, SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial CUresult cuDeviceGetCount(out int count);

    /// <summary>
    /// 按序号获取 CUDA 设备句柄。
    /// </summary>
    /// <param name="dev">输出设备句柄。</param>
    /// <param name="ordinal">设备序号（从 0 起）。</param>
    /// <returns>CUDA 错误码；<see cref="CudaSuccess"/> 表示成功。</returns>
    [LibraryImport(NvcudaLibrary, SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial CUresult cuDeviceGet(out CUdevice dev, int ordinal);

    /// <summary>
    /// 获取指定设备的名称（由原生端写入的以 null 结尾的字节序列）。
    /// </summary>
    /// <param name="name">输出名称缓冲区，由调用方分配。原生端以 ASCII 字节写入。</param>
    /// <param name="len">缓冲区长度（字节数）。</param>
    /// <param name="dev">设备句柄。</param>
    /// <returns>CUDA 错误码；<see cref="CudaSuccess"/> 表示成功。</returns>
    /// <remarks>
    /// 此处使用 <see cref="T:System.Byte[]"/> 而非 <see cref="T:System.Text.StringBuilder"/>，因为
    /// <see cref="LibraryImportAttribute"/> 源生成器不支持 <see cref="T:System.Text.StringBuilder"/> 的封送。
    /// 字节到字符串的解码由 <see cref="DefaultCudaNativeMethods"/> 负责。
    /// </remarks>
    [LibraryImport(NvcudaLibrary, SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial CUresult cuDeviceGetName(byte[] name, int len, CUdevice dev);

    /// <summary>
    /// 获取指定设备的 Compute Capability 主次版本。
    /// </summary>
    /// <param name="major">输出主版本。</param>
    /// <param name="minor">输出次版本。</param>
    /// <param name="dev">设备句柄。</param>
    /// <returns>CUDA 错误码；<see cref="CudaSuccess"/> 表示成功。</returns>
    [LibraryImport(NvcudaLibrary, SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial CUresult cuDeviceComputeCapability(out int major, out int minor, CUdevice dev);
}
