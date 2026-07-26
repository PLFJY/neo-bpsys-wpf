namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 检测旧版 v2 应用设置 JSON。
/// </summary>
public interface ILegacyV2ConfigDetector
{
    /// <summary>
    /// 返回给定的配置 JSON 是否使用旧版 v2 设置结构。
    /// </summary>
    /// <param name="configJson">配置 JSON 文本。</param>
    /// <returns>当 JSON 需要从 v2 迁移时返回 <see langword="true"/>。</returns>
    /// <exception cref="System.Text.Json.JsonException">当 <paramref name="configJson"/> 不是有效的 JSON 时抛出。</exception>
    bool IsLegacyV2Config(string configJson);
}
