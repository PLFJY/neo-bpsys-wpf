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

    /// <summary>
    /// 以代码方式构建一份 GameData 默认配置。
    /// 用于资源文件缺失时的兜底，保证功能不因部署问题完全失效。
    /// </summary>
    public static SmartBpRegionProfile CreateGameDataDefaultProfile()
    {
        // 5 个数字列的相对坐标模板（相对每一行大框）。
        var dataColumns = new[]
        {
            new RelativeRect(0.255, 0, 0.1, 1),
            new RelativeRect(0.4, 0, 0.1, 1),
            new RelativeRect(0.515, 0, 0.1, 1),
            new RelativeRect(0.65, 0, 0.1, 1),
            new RelativeRect(0.79, 0, 0.1, 1)
        };

        var rows = new List<SmartBpRegionRow>();
        // 先定义整块表格区域，再通过“父子相对坐标”生成每一行全局坐标。
        var tableRect = new RelativeRect(0.1, 0.165, 0.845, 0.62);
        rows.Add(CreateRow("row0_hunter", tableRect, new RelativeRect(0, 0, 1, 0.16), new RelativeRect(0, 0, 0.4, 0.5), dataColumns));

        // 求生者 4 行按固定步进从模板扩展。
        const double rowStep = 0.19;
        var survivorTemplate = new RelativeRect(0, 0.279, 1, 0.14);
        for (var i = 0; i < 4; i++)
        {
            var row = survivorTemplate with { Y = survivorTemplate.Y + i * rowStep };
            rows.Add(CreateRow($"row{i + 1}_survivor", tableRect, row, new RelativeRect(0, 0, 0.28, 0.47), dataColumns));
        }

        return new SmartBpRegionProfile
        {
            Version = 1,
            Scene = "GameData",
            BaseAspectRatio = "16:9",
            BaseSize = new WindowSize(16, 9),
            Rows = rows
        };
    }

    /// <summary>
    /// 生成单行配置：1 个名称框 + 5 个数据框。
    /// </summary>
    private static SmartBpRegionRow CreateRow(
        string id,
        RelativeRect tableRect,
        RelativeRect rowInTable,
        RelativeRect nameRect,
        IReadOnlyList<RelativeRect> dataColumns)
    {
        var rowGlobal = ConvertNestedRect(tableRect, rowInTable);
        return new SmartBpRegionRow
        {
            Id = id,
            BigRect = rowGlobal,
            Cells =
            [
                new SmartBpRegionCell { Id = "name", Rect = nameRect },
                new SmartBpRegionCell { Id = "d1", Rect = dataColumns[0] },
                new SmartBpRegionCell { Id = "d2", Rect = dataColumns[1] },
                new SmartBpRegionCell { Id = "d3", Rect = dataColumns[2] },
                new SmartBpRegionCell { Id = "d4", Rect = dataColumns[3] },
                new SmartBpRegionCell { Id = "d5", Rect = dataColumns[4] }
            ]
        };
    }

    /// <summary>
    /// 将子矩形（相对父区域）换算为全局相对矩形（相对整帧）。
    /// </summary>
    private static RelativeRect ConvertNestedRect(RelativeRect parent, RelativeRect child)
    {
        return new RelativeRect(
            parent.X + parent.W * child.X,
            parent.Y + parent.H * child.Y,
            parent.W * child.W,
            parent.H * child.H);
    }
}
