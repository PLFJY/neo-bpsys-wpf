using System;
using System.Collections.Generic;
using System.Text;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Services.Cuda;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services.Cuda;

/// <summary>
/// <see cref="CudaDeviceDetector"/> 的单元测试。
/// 使用 <see cref="StubCudaNativeMethods"/> 模拟 CUDA Driver API，覆盖初始化失败、设备枚举、
/// Compute Capability 支持判断、驱动版本解析以及异常容错场景。
/// 不要求测试机器有 NVIDIA GPU。
/// </summary>
public sealed class CudaDeviceDetectorTest
{
    [Fact]
    public void DetectDevices_CuInitFails_ReturnsEmptyList()
    {
        var stub = new StubCudaNativeMethods { InitReturn = 1 };

        var detector = new CudaDeviceDetector(stub, null);
        var devices = detector.DetectDevices();

        Assert.Empty(devices);
    }

    [Fact]
    public void DetectDevices_CuDriverGetVersionFails_ReturnsEmptyList()
    {
        var stub = new StubCudaNativeMethods { DriverVersionReturn = 1 };

        var detector = new CudaDeviceDetector(stub, null);
        var devices = detector.DetectDevices();

        Assert.Empty(devices);
    }

    [Fact]
    public void DetectDevices_CuDeviceGetCountFails_ReturnsEmptyList()
    {
        var stub = new StubCudaNativeMethods { DeviceCountReturn = 1 };

        var detector = new CudaDeviceDetector(stub, null);
        var devices = detector.DetectDevices();

        Assert.Empty(devices);
    }

    [Fact]
    public void DetectDevices_NoDevices_ReturnsEmptyList()
    {
        var stub = new StubCudaNativeMethods { DeviceCount = 0 };

        var detector = new CudaDeviceDetector(stub, null);
        var devices = detector.DetectDevices();

        Assert.Empty(devices);
    }

    [Fact]
    public void DetectDevices_SingleSupportedDevice_ReturnsOneSupportedDevice()
    {
        var stub = new StubCudaNativeMethods
        {
            DeviceCount = 1,
            Devices =
            [
                new StubDevice { DeviceId = 0, Name = "NVIDIA GeForce RTX 3060", CcMajor = 8, CcMinor = 6 }
            ]
        };

        var detector = new CudaDeviceDetector(stub, null);
        var devices = detector.DetectDevices();

        var device = Assert.Single(devices);
        Assert.Equal(0, device.DeviceId);
        Assert.Equal("NVIDIA GeForce RTX 3060", device.DeviceName);
        Assert.Equal(8, device.ComputeCapabilityMajor);
        Assert.Equal(6, device.ComputeCapabilityMinor);
        Assert.True(device.IsSupported);
    }

    [Fact]
    public void DetectDevices_SingleUnsupportedDevice_ReturnsOneUnsupportedDevice()
    {
        var stub = new StubCudaNativeMethods
        {
            DeviceCount = 1,
            Devices =
            [
                new StubDevice { DeviceId = 0, Name = "NVIDIA GeForce GTX 750", CcMajor = 5, CcMinor = 0 }
            ]
        };

        var detector = new CudaDeviceDetector(stub, null);
        var devices = detector.DetectDevices();

        var device = Assert.Single(devices);
        Assert.Equal(5, device.ComputeCapabilityMajor);
        Assert.Equal(0, device.ComputeCapabilityMinor);
        Assert.False(device.IsSupported);
    }

    [Fact]
    public void DetectDevices_TwoSupportedDevices_ReturnsTwoSupportedDevices()
    {
        var stub = new StubCudaNativeMethods
        {
            DeviceCount = 2,
            Devices =
            [
                new StubDevice { DeviceId = 0, Name = "RTX 3060", CcMajor = 8, CcMinor = 6 },
                new StubDevice { DeviceId = 1, Name = "RTX 2070", CcMajor = 7, CcMinor = 5 }
            ]
        };

        var detector = new CudaDeviceDetector(stub, null);
        var devices = detector.DetectDevices();

        Assert.Equal(2, devices.Count);
        Assert.All(devices, d => Assert.True(d.IsSupported));
        Assert.Equal("RTX 3060", devices[0].DeviceName);
        Assert.Equal("RTX 2070", devices[1].DeviceName);
    }

    [Theory]
    [InlineData(12000, 12, 0)]
    [InlineData(12020, 12, 2)]
    [InlineData(11000, 11, 0)]
    [InlineData(10010, 10, 1)]
    public void DetectDevices_DriverVersion_ParsedCorrectly(int rawVersion, int expectedMajor, int expectedMinor)
    {
        var stub = new StubCudaNativeMethods
        {
            DriverVersion = rawVersion,
            DeviceCount = 1,
            Devices =
            [
                new StubDevice { DeviceId = 0, Name = "GPU", CcMajor = 8, CcMinor = 6 }
            ]
        };

        var detector = new CudaDeviceDetector(stub, null);
        var devices = detector.DetectDevices();

        var device = Assert.Single(devices);
        Assert.Equal(expectedMajor, device.CudaDriverVersion.Major);
        Assert.Equal(expectedMinor, device.CudaDriverVersion.Minor);
    }

    [Fact]
    public void DetectDevices_DeviceName_SetCorrectlyFromCuDeviceGetName()
    {
        const string expectedName = "NVIDIA GeForce RTX 4090";
        var stub = new StubCudaNativeMethods
        {
            DeviceCount = 1,
            Devices =
            [
                new StubDevice { DeviceId = 0, Name = expectedName, CcMajor = 8, CcMinor = 9 }
            ]
        };

        var detector = new CudaDeviceDetector(stub, null);
        var devices = detector.DetectDevices();

        var device = Assert.Single(devices);
        Assert.Equal(expectedName, device.DeviceName);
    }

    [Fact]
    public void DetectDevices_CuDeviceGetFails_SkipsDeviceAndContinues()
    {
        var stub = new StubCudaNativeMethods
        {
            DeviceCount = 2,
            Devices =
            [
                new StubDevice { DeviceId = 0, Name = "Failed GPU", CcMajor = 8, CcMinor = 6, GetReturn = 1 },
                new StubDevice { DeviceId = 1, Name = "RTX 3060", CcMajor = 8, CcMinor = 6, GetReturn = 0 }
            ]
        };

        var detector = new CudaDeviceDetector(stub, null);
        var devices = detector.DetectDevices();

        var device = Assert.Single(devices);
        Assert.Equal(1, device.DeviceId);
        Assert.Equal("RTX 3060", device.DeviceName);
    }

    [Fact]
    public void DetectDevices_CuDeviceGetNameFails_SkipsDeviceAndContinues()
    {
        var stub = new StubCudaNativeMethods
        {
            DeviceCount = 2,
            Devices =
            [
                new StubDevice { DeviceId = 0, Name = "Failed GPU", CcMajor = 8, CcMinor = 6, GetNameReturn = 1 },
                new StubDevice { DeviceId = 1, Name = "RTX 3060", CcMajor = 8, CcMinor = 6, GetNameReturn = 0 }
            ]
        };

        var detector = new CudaDeviceDetector(stub, null);
        var devices = detector.DetectDevices();

        var device = Assert.Single(devices);
        Assert.Equal(1, device.DeviceId);
    }

    [Fact]
    public void DetectDevices_CuDeviceComputeCapabilityFails_SkipsDeviceAndContinues()
    {
        var stub = new StubCudaNativeMethods
        {
            DeviceCount = 2,
            Devices =
            [
                new StubDevice { DeviceId = 0, Name = "Failed GPU", CcMajor = 8, CcMinor = 6, GetCcReturn = 1 },
                new StubDevice { DeviceId = 1, Name = "RTX 3060", CcMajor = 8, CcMinor = 6, GetCcReturn = 0 }
            ]
        };

        var detector = new CudaDeviceDetector(stub, null);
        var devices = detector.DetectDevices();

        var device = Assert.Single(devices);
        Assert.Equal(1, device.DeviceId);
    }

    [Fact]
    public void DetectDevices_DllNotFoundException_ReturnsEmptyList()
    {
        var stub = new StubCudaNativeMethods
        {
            ExceptionToThrow = new DllNotFoundException("nvcuda.dll")
        };

        var detector = new CudaDeviceDetector(stub, null);
        var devices = detector.DetectDevices();

        Assert.Empty(devices);
    }

    [Fact]
    public void DetectDevices_EntryPointNotFoundException_ReturnsEmptyList()
    {
        var stub = new StubCudaNativeMethods
        {
            ExceptionToThrow = new EntryPointNotFoundException("cuInit")
        };

        var detector = new CudaDeviceDetector(stub, null);
        var devices = detector.DetectDevices();

        Assert.Empty(devices);
    }

    /// <summary>
    /// <see cref="ICudaNativeMethods"/> 的可配置 stub 实现，模拟 CUDA Driver API 调用。
    /// </summary>
    private sealed class StubCudaNativeMethods : ICudaNativeMethods
    {
        public int InitReturn { get; set; } = CudaNativeMethods.CudaSuccess;
        public int DriverVersion { get; set; } = 12000;
        public int DriverVersionReturn { get; set; } = CudaNativeMethods.CudaSuccess;
        public int DeviceCount { get; set; } = 0;
        public int DeviceCountReturn { get; set; } = CudaNativeMethods.CudaSuccess;
        public List<StubDevice> Devices { get; set; } = new();
        public Exception? ExceptionToThrow { get; set; }

        public int CuInit(uint flags)
        {
            if (ExceptionToThrow is not null) throw ExceptionToThrow;
            return InitReturn;
        }

        public int CuDriverGetVersion(out int driverVersion)
        {
            driverVersion = DriverVersion;
            return DriverVersionReturn;
        }

        public int CuDeviceGetCount(out int count)
        {
            count = DeviceCount;
            return DeviceCountReturn;
        }

        public int CuDeviceGet(out int device, int ordinal)
        {
            if (ordinal >= 0 && ordinal < Devices.Count)
            {
                device = Devices[ordinal].DeviceId;
                return Devices[ordinal].GetReturn;
            }

            device = -1;
            return 1;
        }

        public int CuDeviceGetName(StringBuilder name, int len, int device)
        {
            var dev = Devices.Find(d => d.DeviceId == device);
            if (dev is null)
            {
                name.Clear();
                return 1;
            }

            name.Clear();
            name.Append(dev.Name);
            return dev.GetNameReturn;
        }

        public int CuDeviceComputeCapability(out int major, out int minor, int device)
        {
            var dev = Devices.Find(d => d.DeviceId == device);
            if (dev is null)
            {
                major = 0;
                minor = 0;
                return 1;
            }

            major = dev.CcMajor;
            minor = dev.CcMinor;
            return dev.GetCcReturn;
        }
    }

    /// <summary>
    /// 单个 CUDA 设备的 stub 配置。
    /// </summary>
    private sealed class StubDevice
    {
        public int DeviceId { get; set; }
        public string Name { get; set; } = "NVIDIA GPU";
        public int CcMajor { get; set; } = 8;
        public int CcMinor { get; set; } = 6;
        public int GetReturn { get; set; } = CudaNativeMethods.CudaSuccess;
        public int GetNameReturn { get; set; } = CudaNativeMethods.CudaSuccess;
        public int GetCcReturn { get; set; } = CudaNativeMethods.CudaSuccess;
    }
}
