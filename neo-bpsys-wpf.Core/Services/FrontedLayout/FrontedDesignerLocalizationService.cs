using System.Text.RegularExpressions;
using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 宿主未提供 i18n 资源时使用的回退设计器 v3 本地化服务。
/// </summary>
public class FrontedDesignerLocalizationService : IFrontedDesignerLocalizationService
{
    public virtual string GetPropertyDisplayName(string propertyName) =>
        GetLocalizedOrFallback($"Designer.Property.{propertyName}", propertyName);

    public virtual string GetPropertyDescription(string propertyName) =>
        GetLocalizedOrFallback($"Designer.PropertyDescription.{propertyName}", string.Empty);

    public virtual string GetGroupDisplayName(string groupName) =>
        GetLocalizedOrFallback($"Designer.PropertyGroup.{groupName}", groupName);

    public virtual string GetControlTypeDisplayName(string controlType) =>
        GetLocalizedOrFallback($"Designer.ControlType.{controlType}", controlType);

    public virtual string GetWindowDisplayName(string windowTypeName) =>
        GetLocalizedOrFallback($"Designer.Window.{windowTypeName}", windowTypeName);

    public virtual string GetCanvasDisplayName(string canvasName) =>
        GetLocalizedOrFallback($"Designer.Canvas.{canvasName}", canvasName);

    public virtual string GetOptionDisplayName(string propertyName, object? value)
    {
        var rawValue = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        return GetLocalizedOrFallback($"Designer.Option.{propertyName}.{rawValue}", rawValue);
    }

    public virtual string GetBindingNodeDisplayName(string pathOrPropertyName, string? fullPath = null)
    {
        // Level 1: 全路径查询。若 fullPath 为空则跳过本级别。
        if (!string.IsNullOrWhiteSpace(fullPath))
        {
            var level1 = GetLocalizedOrFallback($"Designer.Binding.{fullPath}", string.Empty);
            if (!string.IsNullOrEmpty(level1))
            {
                return level1;
            }
        }

        // Level 2: 去索引路径查询。仅当 fullPath 含 [数字] 段且节点本身不是索引标签时执行。
        if (!string.IsNullOrWhiteSpace(fullPath) && !pathOrPropertyName.StartsWith("["))
        {
            var stripped = StripCollectionIndices(fullPath);
            if (stripped != fullPath)
            {
                var level2 = GetLocalizedOrFallback($"Designer.Binding.{stripped}", string.Empty);
                if (!string.IsNullOrEmpty(level2))
                {
                    return level2;
                }
            }
        }

        // Level 3: 属性名查询。
        var level3 = GetLocalizedOrFallback($"Designer.Binding.{pathOrPropertyName}", string.Empty);
        if (!string.IsNullOrEmpty(level3))
        {
            return level3;
        }

        // 全部未命中，回退原始属性名/路径文本。
        return pathOrPropertyName;
    }

    /// <summary>
    /// 从绑定路径中移除集合索引段（如 <c>[0]</c>、<c>[15]</c>），返回清理后的路径。
    /// </summary>
    /// <param name="path">可能包含集合索引段的绑定路径，例如 <c>CurrentGame.SurPlayerList[0].Member.Name</c>。</param>
    /// <returns>移除所有 <c>[数字]</c> 段后的路径，例如 <c>CurrentGame.SurPlayerList.Member.Name</c>。</returns>
    private static string StripCollectionIndices(string path) =>
        Regex.Replace(path, @"\[\d+\]", string.Empty);

    public virtual string GetBindingTypeDisplayName(string typeName) =>
        GetLocalizedOrFallback($"Designer.BindingType.{typeName}", typeName);

    public virtual string GetDesignerText(string key, string fallback) =>
        GetLocalizedOrFallback(key, fallback);

    protected virtual string GetLocalizedOrFallback(string key, string fallback) => fallback;
}
