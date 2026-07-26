using System;
using System.Collections.Generic;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Services.PaddleRuntime;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services.PaddleRuntime;

/// <summary>
/// <see cref="PaddleRuntimeState"/> 的单元测试。
/// 覆盖初始状态、internal 状态写入方法的行为、StateChanged 事件触发以及 RestartRequired 转发。
/// </summary>
public sealed class PaddleRuntimeStateTest
{
    private static IGlobalRestartService CreateRestartService(bool isRestartRequired = false)
    {
        var mock = new Mock<IGlobalRestartService>();
        mock.SetupGet(s => s.IsRestartRequired).Returns(isRestartRequired);
        return mock.Object;
    }

    private static CudaDeviceInfo CreateDeviceInfo(int id, string name, int major, int minor, bool supported = true)
    {
        return new CudaDeviceInfo(
            DeviceId: id,
            DeviceName: name,
            ComputeCapabilityMajor: major,
            ComputeCapabilityMinor: minor,
            CudaDriverVersion: new Version(12, 0),
            IsSupported: supported);
    }

    [Fact]
    public void Constructor_NullGlobalRestartService_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new PaddleRuntimeState(null!));
    }

    [Fact]
    public void InitialState_HasExpectedDefaults()
    {
        var state = new PaddleRuntimeState(CreateRestartService());

        Assert.Equal(OcrInferenceBackend.Cpu, state.ActiveBackend);
        Assert.Equal(0, state.ActiveCudaDeviceId);
        Assert.Null(state.LoadedNativeModulePath);
        Assert.Empty(state.DetectedCudaDevices);
        Assert.Null(state.SelectedCudaDevice);
        Assert.False(state.CudaRuntimeInstalled);
        Assert.False(state.CudaRuntimeCompatible);
        Assert.Null(state.RuntimeLoadError);
        Assert.False(state.RestartRequired);
    }

    [Fact]
    public void SetActiveBackend_Cuda_UpdatesBackendDeviceIdAndPath()
    {
        var state = new PaddleRuntimeState(CreateRestartService());

        state.SetActiveBackend(OcrInferenceBackend.Cuda, 1, @"C:\runtime\paddle_inference_c.dll");

        Assert.Equal(OcrInferenceBackend.Cuda, state.ActiveBackend);
        Assert.Equal(1, state.ActiveCudaDeviceId);
        Assert.Equal(@"C:\runtime\paddle_inference_c.dll", state.LoadedNativeModulePath);
    }

    [Fact]
    public void SetActiveBackend_Cpu_ClearsDeviceIdAndPath()
    {
        var state = new PaddleRuntimeState(CreateRestartService());
        state.SetActiveBackend(OcrInferenceBackend.Cuda, 2, @"C:\path\cuda.dll");

        state.SetActiveBackend(OcrInferenceBackend.Cpu, 0, null);

        Assert.Equal(OcrInferenceBackend.Cpu, state.ActiveBackend);
        Assert.Equal(0, state.ActiveCudaDeviceId);
        Assert.Null(state.LoadedNativeModulePath);
    }

    [Fact]
    public void SetActiveBackend_RaisesStateChanged()
    {
        var state = new PaddleRuntimeState(CreateRestartService());
        var raised = 0;
        state.StateChanged += (_, _) => raised++;

        state.SetActiveBackend(OcrInferenceBackend.Cuda, 0, "path");

        Assert.Equal(1, raised);
    }

    [Fact]
    public void SetDetectedDevices_UpdatesDevicesAndSelected()
    {
        var state = new PaddleRuntimeState(CreateRestartService());
        var devices = new List<CudaDeviceInfo>
        {
            CreateDeviceInfo(0, "RTX 3060", 8, 6),
            CreateDeviceInfo(1, "RTX 4090", 8, 9)
        };
        var selected = devices[1];

        state.SetDetectedDevices(devices, selected);

        Assert.Equal(2, state.DetectedCudaDevices.Count);
        Assert.Same(selected, state.SelectedCudaDevice);
        Assert.Equal("RTX 4090", state.SelectedCudaDevice!.DeviceName);
    }

    [Fact]
    public void SetDetectedDevices_NullDevices_TreatedAsEmpty()
    {
        var state = new PaddleRuntimeState(CreateRestartService());

        state.SetDetectedDevices(null!, null);

        Assert.Empty(state.DetectedCudaDevices);
        Assert.Null(state.SelectedCudaDevice);
    }

    [Fact]
    public void SetDetectedDevices_RaisesStateChanged()
    {
        var state = new PaddleRuntimeState(CreateRestartService());
        var raised = 0;
        state.StateChanged += (_, _) => raised++;

        state.SetDetectedDevices(new List<CudaDeviceInfo> { CreateDeviceInfo(0, "GPU", 8, 6) }, null);

        Assert.Equal(1, raised);
    }

    [Fact]
    public void SetCudaRuntimeStatus_UpdatesInstalledAndCompatibleFlags()
    {
        var state = new PaddleRuntimeState(CreateRestartService());

        state.SetCudaRuntimeStatus(true, true);

        Assert.True(state.CudaRuntimeInstalled);
        Assert.True(state.CudaRuntimeCompatible);
    }

    [Fact]
    public void SetCudaRuntimeStatus_RaisesStateChanged()
    {
        var state = new PaddleRuntimeState(CreateRestartService());
        var raised = 0;
        state.StateChanged += (_, _) => raised++;

        state.SetCudaRuntimeStatus(true, false);

        Assert.Equal(1, raised);
    }

    [Fact]
    public void SetRuntimeLoadError_UpdatesError()
    {
        var state = new PaddleRuntimeState(CreateRestartService());

        state.SetRuntimeLoadError("Failed to load paddle_inference_c.dll");

        Assert.Equal("Failed to load paddle_inference_c.dll", state.RuntimeLoadError);
    }

    [Fact]
    public void SetRuntimeLoadError_Null_ClearsError()
    {
        var state = new PaddleRuntimeState(CreateRestartService());
        state.SetRuntimeLoadError("some error");

        state.SetRuntimeLoadError(null);

        Assert.Null(state.RuntimeLoadError);
    }

    [Fact]
    public void SetRuntimeLoadError_RaisesStateChanged()
    {
        var state = new PaddleRuntimeState(CreateRestartService());
        var raised = 0;
        state.StateChanged += (_, _) => raised++;

        state.SetRuntimeLoadError("error");

        Assert.Equal(1, raised);
    }

    [Fact]
    public void RaiseStateChanged_DirectlyRaisesEvent()
    {
        var state = new PaddleRuntimeState(CreateRestartService());
        var raised = 0;
        state.StateChanged += (_, _) => raised++;

        state.RaiseStateChanged();

        Assert.Equal(1, raised);
    }

    [Fact]
    public void RestartRequired_True_ForwardsToGlobalRestartService()
    {
        var state = new PaddleRuntimeState(CreateRestartService(isRestartRequired: true));

        Assert.True(state.RestartRequired);
    }

    [Fact]
    public void RestartRequired_False_ForwardsToGlobalRestartService()
    {
        var state = new PaddleRuntimeState(CreateRestartService(isRestartRequired: false));

        Assert.False(state.RestartRequired);
    }

    [Fact]
    public void StateChanged_MultipleSubscribers_AllInvoked()
    {
        var state = new PaddleRuntimeState(CreateRestartService());
        var count1 = 0;
        var count2 = 0;
        state.StateChanged += (_, _) => count1++;
        state.StateChanged += (_, _) => count2++;

        state.SetActiveBackend(OcrInferenceBackend.Cuda, 0, "path");

        Assert.Equal(1, count1);
        Assert.Equal(1, count2);
    }
}
