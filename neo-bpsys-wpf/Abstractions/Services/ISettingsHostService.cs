using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Models;

namespace neo_bpsys_wpf.Abstractions.Services;

/// <summary>
/// 应用设置服务接口
/// </summary>
public interface ISettingsHostService
{
    /// <summary>
    /// 设置项
    /// </summary>
    Settings Settings { get; set; }
    /// <summary>
    /// 保存配置
    /// </summary>
    void SaveConfig();
    /// <summary>
    /// 读取配置
    /// </summary>
    void LoadConfig();
    /// <summary>
    /// 重置配置
    /// </summary>
    void ResetConfig();
    /// <summary>
    /// 重置指定窗口的配置
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    void ResetConfig(FrontWindowType windowType);
}