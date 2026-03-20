using neo_bpsys_wpf.Core.Models;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// SmartBp 场景定义。
/// 负责同一场景下的默认配置、编辑布局构建与结构校验规则。
/// </summary>
public interface ISmartBpSceneDefinition
{
    /// <summary>
    /// 场景键（例如 GameData）。
    /// </summary>
    string SceneKey { get; }

    /// <summary>
    /// 生成场景默认配置（优先资源默认，缺失时回退代码兜底）。
    /// </summary>
    SmartBpRegionProfile CreateDefaultProfile();

    /// <summary>
    /// 将存储布局转换为编辑器可直接展示的布局（含本地化展示名）。
    /// </summary>
    RegionLayoutDefinition BuildEditorLayout(RegionLayoutDefinition sourceLayout);

    /// <summary>
    /// 校验编辑器回传布局是否满足场景结构约束。
    /// </summary>
    bool TryValidateEditedLayout(RegionLayoutDefinition layout, out string errorMessage);

    /// <summary>
    /// 将编辑器回传布局标准化为可持久化结构（写回本地化 key 而非翻译文案）。
    /// </summary>
    RegionLayoutDefinition NormalizeEditedLayoutForPersistence(RegionLayoutDefinition layout);

    /// <summary>
    /// 校验完整配置模型是否合法。
    /// </summary>
    bool TryValidateProfile(SmartBpRegionProfile profile, out string errorMessage);
}
