using System.IO;
using System.Text.Json;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Models;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// SmartBp 内置默认识别区域模板。
/// 当前只提供 GameData 的 16:9 默认配置，主要用于“重置配置”场景。
/// </summary>
public static class SmartBpRegionDefaults
{
    /// <summary>
    /// 内置默认 JSON 文件的相对资源路径（相对于应用 Resources 根目录）。
    /// </summary>
    public const string BuiltinGameDataDefaultRelativePath = "SmartBpDefaultConfigs/GameDataRegions.16-9.default.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// 以代码方式构建一份 GameData 默认配置。
    /// 优先读取 Resources 内置默认文件，读取失败时回退到代码兜底模板。
    /// </summary>
    public static SmartBpRegionProfile CreateGameDataDefaultProfile()
    {
        var resourceProfile = TryLoadBuiltinDefaultProfile();
        if (resourceProfile != null)
            return resourceProfile;

        return CreateFallbackProfile();
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

            // 只做最小结构校验，详细校验仍由配置服务统一处理。
            if (!string.Equals(profile.Scene, "GameData", StringComparison.OrdinalIgnoreCase))
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

    /// <summary>
    /// 代码内兜底模板（仅在资源文件缺失或损坏时使用）。
    /// 数值与 Resources/SmartBpDefaultConfigs/GameDataRegions.16-9.default.json 保持一致。
    /// </summary>
    private static SmartBpRegionProfile CreateFallbackProfile()
    {
        var layoutBuilder = RegionLayoutDefinition.Builder("SmartBpSceneGameData");
        AddHunterRow(layoutBuilder);
        AddSurvivorRow(layoutBuilder, "row1_survivor", "SmartBpRegionSurvivorRow1", 33.798);
        AddSurvivorRow(layoutBuilder, "row2_survivor", "SmartBpRegionSurvivorRow2", 45.578);
        AddSurvivorRow(layoutBuilder, "row3_survivor", "SmartBpRegionSurvivorRow3", 57.358);
        AddSurvivorRow(layoutBuilder, "row4_survivor", "SmartBpRegionSurvivorRow4", 69.138);

        return new SmartBpRegionProfile
        {
            Version = 1,
            Scene = "GameData",
            BaseAspectRatio = "16:9",
            BaseSize = new WindowSize(16, 9),
            Layout = layoutBuilder.Build()
        };
    }

    private static void AddHunterRow(RegionLayoutBuilder builder)
    {
        builder.AddNode(
            "row0_hunter",
            "SmartBpRegionHunterRow",
            new RegionNodeConfig { Rect = new RelativeRect(10, 16.5, 84.5, 9.92), ClampToParent = true },
            node =>
            {
                node.AddChild("name", "SmartBpRegionCellName", new RegionNodeConfig { Rect = new RelativeRect(0, 0, 40, 50), ClampToParent = true });
                AddHunterDataCells(node);
            });
    }

    private static void AddSurvivorRow(
        RegionLayoutBuilder builder,
        string rowId,
        string rowLabel,
        double rowY)
    {
        builder.AddTemplatedNode(
            "survivor_rows",
            rowId,
            rowLabel,
            new RegionNodeConfig { Rect = new RelativeRect(10, rowY, 84.5, 8.68), ClampToParent = true },
            node =>
            {
                node.AddChild("name", "SmartBpRegionCellName", new RegionNodeConfig { Rect = new RelativeRect(0, 0, 28, 47), ClampToParent = true });
                AddSurvivorDataCells(node);
            });
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

    /// <summary>
    /// 保留该方法仅用于将来需要继续从父子比例生成全局矩形时复用。
    /// </summary>
    private static void AddRow(
        RegionLayoutBuilder builder,
        string templateGroupId,
        string id,
        string label,
        RelativeRect tableRect,
        RelativeRect rowInTable,
        RelativeRect nameRect,
        IReadOnlyList<RelativeRect> dataColumns)
    {
        var rowGlobal = ConvertNestedRect(tableRect, rowInTable);
        var rowConfig = new RegionNodeConfig { Rect = rowGlobal, ClampToParent = true };
        Action<RegionNodeBuilder> configure = node => node
            .AddChild("name", "SmartBpRegionCellName", new RegionNodeConfig { Rect = nameRect, ClampToParent = true })
            .AddChild("d1", string.Empty, new RegionNodeConfig { Rect = dataColumns[0], ClampToParent = true })
            .AddChild("d2", string.Empty, new RegionNodeConfig { Rect = dataColumns[1], ClampToParent = true })
            .AddChild("d3", string.Empty, new RegionNodeConfig { Rect = dataColumns[2], ClampToParent = true })
            .AddChild("d4", string.Empty, new RegionNodeConfig { Rect = dataColumns[3], ClampToParent = true })
            .AddChild("d5", string.Empty, new RegionNodeConfig { Rect = dataColumns[4], ClampToParent = true });

        if (string.IsNullOrWhiteSpace(templateGroupId))
        {
            builder.AddNode(id, label, rowConfig, configure);
            return;
        }

        builder.AddTemplatedNode(templateGroupId, id, label, rowConfig, configure);
    }

    /// <summary>
    /// 将子矩形（相对父区域）换算为全局相对矩形（相对整帧）。
    /// </summary>
    private static RelativeRect ConvertNestedRect(RelativeRect parent, RelativeRect child)
    {
        return new RelativeRect(
            parent.X + parent.W * (child.X / 100d),
            parent.Y + parent.H * (child.Y / 100d),
            parent.W * (child.W / 100d),
            parent.H * (child.H / 100d));
    }
}
