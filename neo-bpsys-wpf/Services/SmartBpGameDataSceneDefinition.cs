using System.IO;
using System.Text.Json;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Helpers;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// GameData 场景定义。
/// 将编辑器结构、默认模板和校验规则集中在同一处，避免 VM 与 Service 分散维护。
/// </summary>
public sealed class SmartBpGameDataSceneDefinition : SmartBpSceneDefinitionBase
{
    /// <summary>
    /// 内置默认 JSON 文件的相对资源路径（相对于 Resources 根目录）。
    /// </summary>
    private const string BuiltinGameDataDefaultRelativePath = "SmartBpDefaultConfigs/GameDataRegions.16-9.default.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly RegionEditorStructure EditorStructure = CreateEditorStructure();

    /// <inheritdoc />
    public override string SceneKey => SmartBpSceneKeys.GameData;

    /// <inheritdoc />
    public override SmartBpRegionProfile CreateDefaultProfile()
    {
        var resourceProfile = TryLoadBuiltinDefaultProfile();
        if (resourceProfile != null)
            return resourceProfile;

        // 资源缺失时使用代码模板兜底，保证功能可用。
        return CreateFallbackProfile();
    }

    /// <inheritdoc />
    public override RegionLayoutDefinition BuildEditorLayout(RegionLayoutDefinition sourceLayout)
    {
        var builder = RegionLayoutDefinition.Builder(ResolveLocalizedOrRaw(EditorStructure.SceneDisplayName));
        var elementById = EditorStructure.Elements.ToDictionary(e => e.Id, StringComparer.Ordinal);

        foreach (var sourceRoot in sourceLayout.Roots)
        {
            // 未知节点保留原始 Id，避免编辑器直接丢弃用户历史配置。
            var element = elementById.TryGetValue(sourceRoot.Id, out var found)
                ? found
                : new RegionEditorElement(sourceRoot.Id, sourceRoot.Id, []);
            Action<RegionNodeBuilder> configure = node =>
            {
                for (var c = 0; c < sourceRoot.Children.Count; c++)
                {
                    var label = c < element.CellLabels.Count ? element.CellLabels[c] : sourceRoot.Children[c].Id;
                    node.AddChild(
                        sourceRoot.Children[c].Id,
                        ResolveLocalizedOrRaw(label),
                        new RegionNodeConfig
                        {
                            Rect = sourceRoot.Children[c].Rect,
                            ClampToParent = true
                        });
                }
            };

            var rootConfig = new RegionNodeConfig
            {
                Rect = sourceRoot.Rect,
                ClampToParent = true
            };
            if (string.IsNullOrWhiteSpace(element.TemplateGroupId))
            {
                builder.AddNode(sourceRoot.Id, ResolveLocalizedOrRaw(element.Label), rootConfig, configure);
            }
            else
            {
                builder.AddTemplatedNode(element.TemplateGroupId, sourceRoot.Id, ResolveLocalizedOrRaw(element.Label), rootConfig, configure);
            }
        }

        return builder.Build();
    }

    /// <inheritdoc />
    public override bool TryValidateEditedLayout(RegionLayoutDefinition layout, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (layout.Roots.Count != EditorStructure.Elements.Count)
        {
            errorMessage = string.Format(
                ResolveLocalizedOrRaw("SmartBpRegionLayoutMismatchRootCountFormat"),
                EditorStructure.Elements.Count,
                layout.Roots.Count);
            return false;
        }

        for (var i = 0; i < EditorStructure.Elements.Count; i++)
        {
            var root = layout.Roots[i];
            var expectedId = EditorStructure.Elements[i].Id;
            if (!string.Equals(root.Id, expectedId, StringComparison.Ordinal))
            {
                errorMessage = string.Format(
                    ResolveLocalizedOrRaw("SmartBpRegionLayoutMismatchRootIdFormat"),
                    i + 1,
                    expectedId,
                    root.Id);
                return false;
            }

            var expectedCellCount = EditorStructure.Elements[i].CellLabels.Count;
            if (root.Children.Count != expectedCellCount)
            {
                errorMessage = string.Format(
                    ResolveLocalizedOrRaw("SmartBpRegionLayoutMismatchCellCountFormat"),
                    i + 1,
                    expectedCellCount,
                    root.Children.Count);
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public override RegionLayoutDefinition NormalizeEditedLayoutForPersistence(RegionLayoutDefinition layout)
    {
        var builder = RegionLayoutDefinition.Builder(EditorStructure.SceneDisplayName);

        // 布局已通过结构校验，因此按固定顺序重建可稳定写回 key。
        for (var i = 0; i < layout.Roots.Count; i++)
        {
            var sourceRoot = layout.Roots[i];
            var element = EditorStructure.Elements[i];
            Action<RegionNodeBuilder> configure = node =>
            {
                for (var c = 0; c < sourceRoot.Children.Count; c++)
                {
                    var label = c < element.CellLabels.Count ? element.CellLabels[c] : sourceRoot.Children[c].Id;
                    node.AddChild(
                        sourceRoot.Children[c].Id,
                        label,
                        new RegionNodeConfig
                        {
                            Rect = sourceRoot.Children[c].Rect,
                            ClampToParent = true
                        });
                }
            };

            var rootConfig = new RegionNodeConfig
            {
                Rect = sourceRoot.Rect,
                ClampToParent = true
            };
            if (string.IsNullOrWhiteSpace(element.TemplateGroupId))
            {
                builder.AddNode(sourceRoot.Id, element.Label, rootConfig, configure);
            }
            else
            {
                builder.AddTemplatedNode(element.TemplateGroupId, sourceRoot.Id, element.Label, rootConfig, configure);
            }
        }

        return builder.Build();
    }

    /// <inheritdoc />
    public override bool TryValidateProfile(SmartBpRegionProfile profile, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (!string.Equals(profile.Scene, SceneKey, StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = I18nHelper.GetLocalizedString("SmartBpRegionConfigInvalidScene");
            return false;
        }

        if (profile.Layout.Roots.Count != 5)
        {
            errorMessage = I18nHelper.GetLocalizedString("SmartBpRegionConfigInvalidRowCount");
            return false;
        }

        for (var i = 0; i < profile.Layout.Roots.Count; i++)
        {
            var root = profile.Layout.Roots[i];
            if (!root.Rect.IsValid01())
            {
                errorMessage = string.Format(I18nHelper.GetLocalizedString("SmartBpRegionConfigInvalidBigRectFormat"), i + 1);
                return false;
            }

            if (root.Children.Count != 6)
            {
                errorMessage = string.Format(I18nHelper.GetLocalizedString("SmartBpRegionConfigInvalidCellCountFormat"), i + 1);
                return false;
            }

            for (var c = 0; c < root.Children.Count; c++)
            {
                // 小框坐标是相对大框，因此同样按 0~100 百分比校验。
                if (!root.Children[c].Rect.IsValid01())
                {
                    errorMessage = string.Format(I18nHelper.GetLocalizedString("SmartBpRegionConfigInvalidCellRectFormat"), i + 1, c + 1);
                    return false;
                }
            }
        }

        profile.BaseAspectRatio = string.IsNullOrWhiteSpace(profile.BaseAspectRatio)
            ? SmartBpRegionConfigService.ToAspectRatioText(profile.BaseSize.Width, profile.BaseSize.Height)
            : profile.BaseAspectRatio;

        // 存储时仅保留比例基准（如 16x9），避免写入具体像素分辨率。
        if (TryParseAspectIntegerPair(profile.BaseAspectRatio, out var aw, out var ah))
            profile.BaseSize = new WindowSize(aw, ah);

        return true;
    }

    private static SmartBpRegionProfile? TryLoadBuiltinDefaultProfile()
    {
        try
        {
            var path = Path.Combine(AppConstants.ResourcesPath, BuiltinGameDataDefaultRelativePath);
            if (!File.Exists(path))
                return null;

            var json = File.ReadAllText(path);
            var profile = JsonSerializer.Deserialize<SmartBpRegionProfile>(json, JsonOptions);
            if (profile == null)
                return null;

            if (!string.Equals(profile.Scene, SmartBpSceneKeys.GameData, StringComparison.OrdinalIgnoreCase))
                return null;
            if (profile.Layout.Roots.Count == 0)
                return null;

            return profile;
        }
        catch
        {
            return null;
        }
    }

    private static SmartBpRegionProfile CreateFallbackProfile()
    {
        var layoutBuilder = RegionLayoutDefinition.Builder("SmartBpSceneGameData");

        // row0: hunter
        layoutBuilder.AddTemplatedNode(
            "hunter_rows",
            "row0_hunter",
            "SmartBpRegionHunterRow",
            new RegionNodeConfig { Rect = new RelativeRect(10, 16.5, 84.5, 9.92), ClampToParent = true },
            node =>
            {
                node.AddChild("name", "SmartBpRegionCellName", new RegionNodeConfig { Rect = new RelativeRect(0, 0, 40, 50), ClampToParent = true });
                AddHunterDataCells(node);
            });

        // row1-row4: survivor template rows
        layoutBuilder.AddTemplatedNode(
            "survivor_rows",
            "row1_survivor",
            "SmartBpRegionSurvivorRow1",
            new RegionNodeConfig { Rect = new RelativeRect(10, 33.798, 84.5, 8.68), ClampToParent = true },
            node =>
            {
                node.AddChild("name", "SmartBpRegionCellName", new RegionNodeConfig { Rect = new RelativeRect(0, 0, 28, 47), ClampToParent = true });
                AddSurvivorDataCells(node);
            });
        layoutBuilder.AddTemplatedNode(
            "survivor_rows",
            "row2_survivor",
            "SmartBpRegionSurvivorRow2",
            new RegionNodeConfig { Rect = new RelativeRect(10, 45.578, 84.5, 8.68), ClampToParent = true },
            node =>
            {
                node.AddChild("name", "SmartBpRegionCellName", new RegionNodeConfig { Rect = new RelativeRect(0, 0, 28, 47), ClampToParent = true });
                AddSurvivorDataCells(node);
            });
        layoutBuilder.AddTemplatedNode(
            "survivor_rows",
            "row3_survivor",
            "SmartBpRegionSurvivorRow3",
            new RegionNodeConfig { Rect = new RelativeRect(10, 57.358, 84.5, 8.68), ClampToParent = true },
            node =>
            {
                node.AddChild("name", "SmartBpRegionCellName", new RegionNodeConfig { Rect = new RelativeRect(0, 0, 28, 47), ClampToParent = true });
                AddSurvivorDataCells(node);
            });
        layoutBuilder.AddTemplatedNode(
            "survivor_rows",
            "row4_survivor",
            "SmartBpRegionSurvivorRow4",
            new RegionNodeConfig { Rect = new RelativeRect(10, 69.138, 84.5, 8.68), ClampToParent = true },
            node =>
            {
                node.AddChild("name", "SmartBpRegionCellName", new RegionNodeConfig { Rect = new RelativeRect(0, 0, 28, 47), ClampToParent = true });
                AddSurvivorDataCells(node);
            });

        return new SmartBpRegionProfile
        {
            Version = 1,
            Scene = SmartBpSceneKeys.GameData,
            BaseAspectRatio = "16:9",
            BaseSize = new WindowSize(16, 9),
            Layout = layoutBuilder.Build()
        };
    }

    private static void AddHunterDataCells(RegionNodeBuilder node)
    {
        node.AddChild("d1", "SmartBpRegionCellHunterRemainingCipher", new RegionNodeConfig { Rect = new RelativeRect(25.5, 0, 10, 100), ClampToParent = true })
            .AddChild("d2", "SmartBpRegionCellHunterPalletsDestroyed", new RegionNodeConfig { Rect = new RelativeRect(40, 0, 10, 100), ClampToParent = true })
            .AddChild("d3", "SmartBpRegionCellHunterSurvivorHits", new RegionNodeConfig { Rect = new RelativeRect(51.5, 0, 10, 100), ClampToParent = true })
            .AddChild("d4", "SmartBpRegionCellHunterTerrorShocks", new RegionNodeConfig { Rect = new RelativeRect(65, 0, 10, 100), ClampToParent = true })
            .AddChild("d5", "SmartBpRegionCellHunterKnockdowns", new RegionNodeConfig { Rect = new RelativeRect(79, 0, 10, 100), ClampToParent = true });
    }

    private static void AddSurvivorDataCells(RegionNodeBuilder node)
    {
        node.AddChild("d1", "SmartBpRegionCellSurvivorDecodingProgress", new RegionNodeConfig { Rect = new RelativeRect(25.5, 0, 10, 100), ClampToParent = true })
            .AddChild("d2", "SmartBpRegionCellSurvivorPalletStrikes", new RegionNodeConfig { Rect = new RelativeRect(40, 0, 10, 100), ClampToParent = true })
            .AddChild("d3", "SmartBpRegionCellSurvivorRescues", new RegionNodeConfig { Rect = new RelativeRect(51.5, 0, 10, 100), ClampToParent = true })
            .AddChild("d4", "SmartBpRegionCellSurvivorHeals", new RegionNodeConfig { Rect = new RelativeRect(65, 0, 10, 100), ClampToParent = true })
            .AddChild("d5", "SmartBpRegionCellSurvivorContainmentTime", new RegionNodeConfig { Rect = new RelativeRect(79, 0, 10, 100), ClampToParent = true });
    }

    private static bool TryParseAspectIntegerPair(string text, out int w, out int h)
    {
        w = 0;
        h = 0;
        var parts = text.Split(':');
        if (parts.Length != 2)
            return false;

        if (!int.TryParse(parts[0], out w) || !int.TryParse(parts[1], out h))
            return false;

        return w > 0 && h > 0;
    }

    /// <summary>
    /// GameData 编辑结构定义。
    /// </summary>
    private static RegionEditorStructure CreateEditorStructure()
    {
        const string hunterTemplateGroup = "hunter_rows";
        const string survivorTemplateGroup = "survivor_rows";
        return new RegionEditorStructure(
            "SmartBpSceneGameData",
            [
                new RegionEditorElement("row0_hunter", "SmartBpRegionHunterRow",
                    [
                        "SmartBpRegionCellName", "SmartBpRegionCellHunterRemainingCipher",
                        "SmartBpRegionCellHunterPalletsDestroyed", "SmartBpRegionCellHunterSurvivorHits",
                        "SmartBpRegionCellHunterTerrorShocks", "SmartBpRegionCellHunterKnockdowns"
                    ],
                    hunterTemplateGroup),
                new RegionEditorElement("row1_survivor", "SmartBpRegionSurvivorRow1",
                    [
                        "SmartBpRegionCellName", "SmartBpRegionCellSurvivorDecodingProgress",
                        "SmartBpRegionCellSurvivorPalletStrikes", "SmartBpRegionCellSurvivorRescues",
                        "SmartBpRegionCellSurvivorHeals", "SmartBpRegionCellSurvivorContainmentTime"
                    ],
                    survivorTemplateGroup),
                new RegionEditorElement("row2_survivor", "SmartBpRegionSurvivorRow2",
                    [
                        "SmartBpRegionCellName", "SmartBpRegionCellSurvivorDecodingProgress",
                        "SmartBpRegionCellSurvivorPalletStrikes", "SmartBpRegionCellSurvivorRescues",
                        "SmartBpRegionCellSurvivorHeals", "SmartBpRegionCellSurvivorContainmentTime"
                    ],
                    survivorTemplateGroup),
                new RegionEditorElement("row3_survivor", "SmartBpRegionSurvivorRow3",
                    [
                        "SmartBpRegionCellName", "SmartBpRegionCellSurvivorDecodingProgress",
                        "SmartBpRegionCellSurvivorPalletStrikes", "SmartBpRegionCellSurvivorRescues",
                        "SmartBpRegionCellSurvivorHeals", "SmartBpRegionCellSurvivorContainmentTime"
                    ],
                    survivorTemplateGroup),
                new RegionEditorElement("row4_survivor", "SmartBpRegionSurvivorRow4",
                    [
                        "SmartBpRegionCellName", "SmartBpRegionCellSurvivorDecodingProgress",
                        "SmartBpRegionCellSurvivorPalletStrikes", "SmartBpRegionCellSurvivorRescues",
                        "SmartBpRegionCellSurvivorHeals", "SmartBpRegionCellSurvivorContainmentTime"
                    ],
                    survivorTemplateGroup)
            ]);
    }

    private sealed record RegionEditorStructure(string SceneDisplayName, IReadOnlyList<RegionEditorElement> Elements);

    private sealed record RegionEditorElement(
        string Id,
        string Label,
        IReadOnlyList<string> CellLabels,
        string? TemplateGroupId = null);
}
