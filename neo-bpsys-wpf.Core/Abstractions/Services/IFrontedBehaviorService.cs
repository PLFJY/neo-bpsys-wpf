namespace neo_bpsys_wpf.Core.Abstractions.Services;

using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// 处理由前台窗口标识拥有的前台行为数据。
/// </summary>
public interface IFrontedBehaviorService
{
    /// <summary>
    /// 加载指定前台窗口的行为文档。
    /// </summary>
    /// <param name="windowType">完整的窗口类型名。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>行为文档。</returns>
    Task<FrontedBehaviorDocument> LoadDocumentAsync(
        string windowType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 加载指定前台窗口的内置行为文档，而不受当前活动布局包影响。
    /// </summary>
    /// <param name="windowType">完整的窗口类型名。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>内置行为文档；内置文件不存在时返回空文档。</returns>
    Task<FrontedBehaviorDocument> LoadBuiltInDocumentAsync(
        string windowType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存行为文档，对应其 <see cref="FrontedBehaviorDocument.WindowType" />。
    /// </summary>
    /// <param name="document">要保存的行为文档。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task SaveDocumentAsync(
        FrontedBehaviorDocument document,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 移除直接附加到指定行为目标上的行为数据。
    /// </summary>
    /// <param name="behaviorGuid">行为目标 GUID。</param>
    void RemoveBehaviors(Guid behaviorGuid);
}
