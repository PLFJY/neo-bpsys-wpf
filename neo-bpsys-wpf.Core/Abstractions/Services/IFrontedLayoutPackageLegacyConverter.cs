using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// Converts legacy .bpui packages into Designer v3 .bpui packages.
/// </summary>
public interface IFrontedLayoutPackageLegacyConverter
{
    /// <summary>
    /// 将旧版包转换为干净的 v3 包并可选择安装。
    /// </summary>
    /// <param name="request">转换请求参数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>转换结果。</returns>
    Task<FrontedLayoutPackageLegacyConvertResult> ConvertAsync(
        FrontedLayoutPackageLegacyConvertRequest request,
        CancellationToken cancellationToken = default);
}
