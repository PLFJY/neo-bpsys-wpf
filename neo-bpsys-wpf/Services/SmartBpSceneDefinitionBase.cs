using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Helpers;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// SmartBp 场景定义基类。
/// 提供统一的本地化解析能力，具体结构规则由派生类实现。
/// </summary>
public abstract class SmartBpSceneDefinitionBase : ISmartBpSceneDefinition
{
    /// <inheritdoc />
    public abstract string SceneKey { get; }

    /// <inheritdoc />
    public abstract SmartBpRegionProfile CreateDefaultProfile();

    /// <inheritdoc />
    public abstract RegionLayoutDefinition BuildEditorLayout(RegionLayoutDefinition sourceLayout);

    /// <inheritdoc />
    public abstract bool TryValidateEditedLayout(RegionLayoutDefinition layout, out string errorMessage);

    /// <inheritdoc />
    public abstract RegionLayoutDefinition NormalizeEditedLayoutForPersistence(RegionLayoutDefinition layout);

    /// <inheritdoc />
    public abstract bool TryValidateProfile(SmartBpRegionProfile profile, out string errorMessage);

    /// <summary>
    /// 将本地化 key 解析为当前语言文本。
    /// 当 key 不存在时原样返回，保证配置文本可回退。
    /// </summary>
    protected static string ResolveLocalizedOrRaw(string keyOrRawText)
    {
        var localized = I18nHelper.GetLocalizedString(keyOrRawText);
        return string.IsNullOrWhiteSpace(localized) ? keyOrRawText : localized;
    }
}
