using System.Text;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.Services.Cuda;

/// <summary>
/// 可 mock 的 CUDA Driver API 调用抽象，供 <see cref="CudaDeviceDetector"/> 间接调用 <see cref="CudaNativeMethods"/>。
/// </summary>
/// <remarks>
/// 引入此抽象的目的是让 <see cref="CudaDeviceDetector"/> 不直接依赖 P/Invoke 声明，
/// 测试时可注入自定义实现而无需加载 <c>nvcuda.dll</c>。
/// </remarks>
internal interface ICudaNativeMethods
{
    /// <summary>
    /// 初始化 CUDA Driver API 上下文。
    /// </summary>
    /// <param name="flags">初始化标志，当前必须为 0。</param>
    /// <returns>CUDA 错误码；0 表示成功。</returns>
    int CuInit(uint flags);

    /// <summary>
    /// 获取已安装的 CUDA Driver 版本号。
    /// </summary>
    /// <param name="driverVersion">输出驱动版本号（如 12020 表示 12.2）。</param>
    /// <returns>CUDA 错误码；0 表示成功。</returns>
    int CuDriverGetVersion(out int driverVersion);

    /// <summary>
    /// 获取系统中可用的 CUDA 设备数量。
    /// </summary>
    /// <param name="count">输出设备数量。</param>
    /// <returns>CUDA 错误码；0 表示成功。</returns>
    int CuDeviceGetCount(out int count);

    /// <summary>
    /// 按序号获取 CUDA 设备句柄。
    /// </summary>
    /// <param name="device">输出设备句柄。</param>
    /// <param name="ordinal">设备序号（从 0 起）。</param>
    /// <returns>CUDA 错误码；0 表示成功。</returns>
    int CuDeviceGet(out int device, int ordinal);

    /// <summary>
    /// 获取指定设备的名称。
    /// </summary>
    /// <param name="name">输出名称缓冲区，由调用方分配，实现负责在成功时填充。</param>
    /// <param name="len">缓冲区容量。</param>
    /// <param name="device">设备句柄。</param>
    /// <returns>CUDA 错误码；0 表示成功。</returns>
    int CuDeviceGetName(StringBuilder name, int len, int device);

    /// <summary>
    /// 获取指定设备的 Compute Capability 主次版本。
    /// </summary>
    /// <param name="major">输出主版本。</param>
    /// <param name="minor">输出次版本。</param>
    /// <param name="device">设备句柄。</param>
    /// <returns>CUDA 错误码；0 表示成功。</returns>
    int CuDeviceComputeCapability(out int major, out int minor, int device);
}

/// <summary>
/// <see cref="ICudaNativeMethods"/> 的默认实现，将调用转发到 <see cref="CudaNativeMethods"/> 的 P/Invoke 声明。
/// </summary>
internal sealed class DefaultCudaNativeMethods : ICudaNativeMethods
{
    /// <inheritdoc />
    public int CuInit(uint flags) => CudaNativeMethods.cuInit(flags);

    /// <inheritdoc />
    public int CuDriverGetVersion(out int driverVersion) => CudaNativeMethods.cuDriverGetVersion(out driverVersion);

    /// <inheritdoc />
    public int CuDeviceGetCount(out int count) => CudaNativeMethods.cuDeviceGetCount(out count);

    /// <inheritdoc />
    public int CuDeviceGet(out int device, int ordinal) => CudaNativeMethods.cuDeviceGet(out device, ordinal);

    /// <inheritdoc />
    public int CuDeviceGetName(StringBuilder name, int len, int device)
    {
        var buffer = new byte[len];
        var result = CudaNativeMethods.cuDeviceGetName(buffer, len, device);
        name.Clear();
        if (result == CudaNativeMethods.CudaSuccess)
        {
            var str = Encoding.ASCII.GetString(buffer);
            var nullIndex = str.IndexOf('\0');
            if (nullIndex >= 0)
            {
                str = str[..nullIndex];
            }
            name.Append(str);
        }

        return result;
    }

    /// <inheritdoc />
    public int CuDeviceComputeCapability(out int major, out int minor, int device) =>
        CudaNativeMethods.cuDeviceComputeCapability(out major, out minor, device);
}

/// <summary>
/// <see cref="ICudaDeviceDetector"/> 的默认实现，通过 <c>nvcuda.dll</c> 调用 CUDA Driver API 枚举系统中的 NVIDIA CUDA 设备。
/// </summary>
/// <remarks>
/// 当 <c>nvcuda.dll</c> 不存在或任何 P/Invoke 失败时，<see cref="DetectDevices"/> 返回空列表且不抛出异常。
/// 单个设备的查询失败会跳过该设备并继续枚举后续设备。
/// </remarks>
public sealed class CudaDeviceDetector : ICudaDeviceDetector
{
    /// <summary>
    /// 受支持的 Compute Capability (major, minor) 集合。精确匹配主次版本。
    /// </summary>
    private static readonly HashSet<(int Major, int Minor)> SupportedComputeCapabilities = new()
    {
        (6, 1),
        (7, 5),
        (8, 6),
        (8, 9),
        (12, 0)
    };

    /// <summary>
    /// 设备名称缓冲区大小（字节数）。
    /// </summary>
    private const int DeviceNameBufferLength = 256;

    private readonly ICudaNativeMethods _nativeMethods;
    private readonly ILogger<CudaDeviceDetector> _logger;

    /// <summary>
    /// 当调用方未提供 <see cref="ILogger{T}"/> 时使用的空日志记录器，避免依赖
    /// <c>Microsoft.Extensions.Logging.Abstractions</c> 程序集中的 <c>NullLogger&lt;T&gt;</c>
    /// （WPF 临时标记编译项目不传播该包引用）。
    /// </summary>
    private static readonly ILogger<CudaDeviceDetector> NullLoggerInstance = new NullLoggerImpl();

    /// <summary>
    /// 初始化 <see cref="CudaDeviceDetector"/> 使用默认的 <see cref="DefaultCudaNativeMethods"/> 和空日志记录器。
    /// </summary>
    /// <remarks>
    /// 此无参构造函数作为 DI 激活的兜底；当 <c>ILogger&lt;CudaDeviceDetector&gt;</c> 在容器中注册时，
    /// DI 会优先选择 <see cref="CudaDeviceDetector(ILogger{CudaDeviceDetector}?)"/> 注入真实日志记录器。
    /// </remarks>
    public CudaDeviceDetector() : this(nativeMethods: null, logger: null)
    {
    }

    /// <summary>
    /// 初始化 <see cref="CudaDeviceDetector"/>，可指定日志记录器（DI 友好）。
    /// </summary>
    /// <param name="logger">日志记录器；为 <c>null</c> 时使用空日志记录器。</param>
    public CudaDeviceDetector(ILogger<CudaDeviceDetector>? logger) : this(nativeMethods: null, logger)
    {
    }

    /// <summary>
    /// 初始化 <see cref="CudaDeviceDetector"/>，可指定原生 API 入口和日志记录器（测试用）。
    /// </summary>
    /// <param name="nativeMethods">原生 CUDA API 调用入口；为 <c>null</c> 时使用 <see cref="DefaultCudaNativeMethods"/>。</param>
    /// <param name="logger">日志记录器；为 <c>null</c> 时使用空日志记录器。</param>
    internal CudaDeviceDetector(ICudaNativeMethods? nativeMethods, ILogger<CudaDeviceDetector>? logger)
    {
        _nativeMethods = nativeMethods ?? new DefaultCudaNativeMethods();
        _logger = logger ?? NullLoggerInstance;
    }

    /// <inheritdoc />
    public IReadOnlyList<CudaDeviceInfo> DetectDevices()
    {
        try
        {
            var initResult = _nativeMethods.CuInit(0);
            if (initResult != CudaNativeMethods.CudaSuccess)
            {
                _logger.LogWarning("cuInit failed with error code {ErrorCode}. No CUDA devices will be detected.", initResult);
                return Array.Empty<CudaDeviceInfo>();
            }

            var driverResult = _nativeMethods.CuDriverGetVersion(out var driverVersion);
            if (driverResult != CudaNativeMethods.CudaSuccess)
            {
                _logger.LogWarning("cuDriverGetVersion failed with error code {ErrorCode}. No CUDA devices will be detected.", driverResult);
                return Array.Empty<CudaDeviceInfo>();
            }

            var countResult = _nativeMethods.CuDeviceGetCount(out var deviceCount);
            if (countResult != CudaNativeMethods.CudaSuccess)
            {
                _logger.LogWarning("cuDeviceGetCount failed with error code {ErrorCode}. No CUDA devices will be detected.", countResult);
                return Array.Empty<CudaDeviceInfo>();
            }

            var cudaDriverVersion = new Version(driverVersion / 1000, (driverVersion % 1000) / 10);
            _logger.LogInformation(
                "Detected {DeviceCount} CUDA device(s). CUDA Driver version: {DriverVersion} ({Major}.{Minor}).",
                deviceCount,
                driverVersion,
                cudaDriverVersion.Major,
                cudaDriverVersion.Minor);

            var devices = new List<CudaDeviceInfo>(deviceCount);
            for (var i = 0; i < deviceCount; i++)
            {
                try
                {
                    var deviceResult = _nativeMethods.CuDeviceGet(out var device, i);
                    if (deviceResult != CudaNativeMethods.CudaSuccess)
                    {
                        _logger.LogWarning("cuDeviceGet failed for ordinal {Ordinal} with error code {ErrorCode}. Skipping.", i, deviceResult);
                        continue;
                    }

                    var nameBuilder = new StringBuilder(DeviceNameBufferLength);
                    var nameResult = _nativeMethods.CuDeviceGetName(nameBuilder, DeviceNameBufferLength, device);
                    if (nameResult != CudaNativeMethods.CudaSuccess)
                    {
                        _logger.LogWarning("cuDeviceGetName failed for device {DeviceId} with error code {ErrorCode}. Skipping.", device, nameResult);
                        continue;
                    }

                    var ccResult = _nativeMethods.CuDeviceComputeCapability(out var major, out var minor, device);
                    if (ccResult != CudaNativeMethods.CudaSuccess)
                    {
                        _logger.LogWarning("cuDeviceComputeCapability failed for device {DeviceId} with error code {ErrorCode}. Skipping.", device, ccResult);
                        continue;
                    }

                    var isSupported = SupportedComputeCapabilities.Contains((major, minor));
                    var info = new CudaDeviceInfo(
                        DeviceId: device,
                        DeviceName: nameBuilder.ToString(),
                        ComputeCapabilityMajor: major,
                        ComputeCapabilityMinor: minor,
                        CudaDriverVersion: cudaDriverVersion,
                        IsSupported: isSupported);

                    _logger.LogInformation(
                        "CUDA device {DeviceId}: {DeviceName}, Compute Capability {Major}.{Minor}, Driver {DriverVersion}, Supported={IsSupported}.",
                        info.DeviceId,
                        info.DeviceName,
                        info.ComputeCapabilityMajor,
                        info.ComputeCapabilityMinor,
                        info.CudaDriverVersion,
                        info.IsSupported);

                    devices.Add(info);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Unexpected error while querying CUDA device ordinal {Ordinal}. Skipping.", i);
                }
            }

            return devices;
        }
        catch (DllNotFoundException ex)
        {
            _logger.LogWarning(ex, "nvcuda.dll was not found; NVIDIA CUDA driver does not appear to be installed. Returning an empty device list.");
            return Array.Empty<CudaDeviceInfo>();
        }
        catch (EntryPointNotFoundException ex)
        {
            _logger.LogWarning(ex, "A required CUDA Driver API entry point was not found in nvcuda.dll. Returning an empty device list.");
            return Array.Empty<CudaDeviceInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while detecting CUDA devices. Returning an empty device list.");
            return Array.Empty<CudaDeviceInfo>();
        }
    }

    /// <summary>
    /// 最小空日志记录器实现，等价于 <c>NullLogger&lt;T&gt;</c>，但不依赖
    /// <c>Microsoft.Extensions.Logging.Abstractions</c> 程序集（WPF 临时标记编译项目不传播该包引用）。
    /// </summary>
    private sealed class NullLoggerImpl : ILogger<CudaDeviceDetector>
    {
        /// <inheritdoc />
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => false;

        /// <inheritdoc />
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            // 空实现：丢弃所有日志。
        }

        private sealed class NullScope : IDisposable
        {
            /// <summary>
            /// 共享的空 <see cref="IDisposable"/> 实例。
            /// </summary>
            public static readonly NullScope Instance = new();

            /// <inheritdoc />
            public void Dispose()
            {
                // 无操作。
            }
        }
    }
}
