using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 将旧版 .bpui 包转换为设计器 v3 .bpui 包。
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
