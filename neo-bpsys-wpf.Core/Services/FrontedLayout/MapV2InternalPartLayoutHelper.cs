using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 创建并维护 MapV2Display 固定内部部件布局。
/// </summary>
public static class MapV2InternalPartLayoutHelper
{
    /// <summary>
    /// 补齐缺失的内部部件，同时保留已有部件布局。
    /// </summary>
    /// <param name="config">MapV2Display 配置。</param>
    public static void EnsureParts(MapV2DisplayControlConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.InternalParts ??= [];
        var width = Math.Max(1D, config.Width ?? 200D);
        var height = Math.Max(1D, config.Height ?? 155D);
        foreach (var part in CreateDefaultParts(width, height))
        {
            if (config.InternalParts.All(candidate => candidate.Part != part.Part))
            {
                config.InternalParts.Add(part);
            }
        }
    }

    /// <summary>
    /// 创建与旧 MapV2Presenter 视觉结构等价的默认内部布局。
    /// </summary>
    /// <param name="width">父控件宽度。</param>
    /// <param name="height">父控件高度。</param>
    /// <returns>完整的固定内部部件集合。</returns>
    public static IReadOnlyList<MapV2InternalPartLayoutConfig> CreateDefaultParts(double width, double height)
    {
        width = Math.Max(1D, width);
        height = Math.Max(1D, height);
        var teamHeight = height / 3D;
        var mapRowHeight = height * 4D / 9D;
        var campTop = height * 7D / 9D;
        var mapX = Math.Min(5D, width / 2D);
        var mapY = teamHeight + Math.Min(5D, mapRowHeight / 2D);
        var mapWidth = Math.Max(1D, width - mapX * 2D);
        var mapHeight = Math.Max(1D, mapRowHeight - Math.Min(10D, mapRowHeight));
        var mapNameHeight = Math.Min(20D, mapHeight);

        return
        [
            Create(MapV2InternalStylePart.TeamName, 0D, 0D, width, teamHeight),
            Create(MapV2InternalStylePart.MapCard, mapX, mapY, mapWidth, mapHeight),
            Create(MapV2InternalStylePart.MapName, mapX, mapY + mapHeight - mapNameHeight, mapWidth, mapNameHeight),
            Create(MapV2InternalStylePart.CampName, 0D, campTop, width, height - campTop),
            Create(MapV2InternalStylePart.PickingBorder, 0D, 0D, width, height)
        ];
    }

    private static MapV2InternalPartLayoutConfig Create(
        MapV2InternalStylePart part,
        double x,
        double y,
        double width,
        double height) =>
        new() { Part = part, X = x, Y = y, Width = width, Height = height };
}
