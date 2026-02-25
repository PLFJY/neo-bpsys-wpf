using neo_bpsys_wpf.Core.Models;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// SmartBp 识别区域配置服务。
/// 负责 GameData 场景的读取、保存、导入、重置和比例状态计算。
/// </summary>
public interface ISmartBpRegionConfigService
{
    /// <summary>
    /// SmartBp 配置目录（%APPDATA% 下）。
    /// </summary>
    string ConfigDirectoryPath { get; }

    /// <summary>
    /// GameData 配置文件完整路径。
    /// </summary>
    string GameDataConfigPath { get; }

    /// <summary>
    /// 获取当前生效的 GameData 配置（返回副本，避免外部直接改缓存）。
    /// </summary>
    SmartBpRegionProfile GetCurrentGameDataProfile();

    /// <summary>
    /// 保存 GameData 配置。
    /// </summary>
    bool TrySaveGameDataProfile(SmartBpRegionProfile profile, out string errorMessage);

    /// <summary>
    /// 从指定 JSON 导入并应用 GameData 配置。
    /// </summary>
    bool TryImportGameDataProfile(string sourcePath, out string errorMessage);

    /// <summary>
    /// 导出当前生效的 GameData 配置到指定路径。
    /// </summary>
    bool TryExportGameDataProfile(string targetPath, out string errorMessage);

    /// <summary>
    /// 将当前配置重置为程序内置 16:9 默认配置。
    /// </summary>
    bool TryResetGameDataToBuiltinDefault(out string errorMessage);

    /// <summary>
    /// 获取配置比例和当前捕获比例的对比结果。
    /// </summary>
    SmartBpAspectInfo GetAspectInfo(string? captureAspectRatio);

    /// <summary>
    /// 当配置被保存/导入/重置后触发。
    /// </summary>
    event EventHandler? GameDataProfileChanged;
}
