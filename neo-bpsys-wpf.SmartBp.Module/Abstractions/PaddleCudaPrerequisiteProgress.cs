using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.SmartBp.Module.Abstractions;

/// <summary>提供模块内部的 NVIDIA 多组件进度，不扩展宿主的运行时 ABI。</summary>
public interface IPaddleCudaPrerequisiteProgressSource
{
    /// <summary>获取安装状态与当前组件一致的进度快照。</summary>
    PaddleCudaPrerequisiteSetupProgress Progress { get; }
}

/// <summary>将宿主安装状态与模块自己的多组件进度作为一个快照发布。</summary>
/// <param name="Status">现有安装状态。</param>
/// <param name="ComponentProgress">当前组件进度；未在处理组件时为 null。</param>
public sealed record PaddleCudaPrerequisiteSetupProgress(
    PaddleCudaPrerequisiteSetupStatus Status, PaddleCudaPrerequisiteComponentProgress? ComponentProgress);

/// <summary>描述当前 NVIDIA 依赖组件及已校验完成的前序归档。</summary>
/// <param name="Id">官方组件标识。</param>
/// <param name="FileName">当前归档文件名。</param>
/// <param name="Index">当前组件序号，从 1 开始。</param>
/// <param name="Count">本次依赖安装的组件总数。</param>
/// <param name="CompletedBytes">前序已校验归档的总字节数，包含校验通过的缓存。</param>
/// <param name="TotalBytes">整组归档在固定分发清单中的总字节数。</param>
/// <param name="FileSize">当前归档在固定分发清单中的字节数。</param>
/// <param name="FileBytesReceived">下载器报告的当前文件已接收字节数。</param>
/// <param name="FilePercentage">下载器报告的当前文件百分比；未知时为 null。</param>
public sealed record PaddleCudaPrerequisiteComponentProgress(
    string Id, string FileName, int Index, int Count, long CompletedBytes, long TotalBytes,
    long FileSize, long FileBytesReceived, double? FilePercentage)
{
    /// <summary>获取按固定归档大小加权的总下载百分比；校验、解压阶段保持已完成的下载进度。</summary>
    public double OverallPercentage => TotalBytes <= 0 ? 0 : Math.Clamp(
        100 * (CompletedBytes + (FilePercentage is { } percentage
            ? FileSize * Math.Clamp(percentage, 0, 100) / 100
            : Math.Clamp(FileBytesReceived, 0, FileSize))) / TotalBytes, 0, 100);
}
