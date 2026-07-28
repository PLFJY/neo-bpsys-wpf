using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using System.Text;
using System.Text.RegularExpressions;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 旧版布局包转换消息的构建与本地化辅助类。
/// </summary>
public static partial class LegacyConvertMessageHelper
{
    /// <summary>
    /// 本地化委托。接收消息代码，返回本地化模板字符串。
    /// 模板中的 <c>{ArgName}</c> 占位符会被对应的参数值替换。
    /// 若未设置，将使用代码本身作为回退。
    /// </summary>
    public static Func<string, string>? LocalizeTemplate { get; set; }

    /// <summary>
    /// 代码常量 —— 旧版包中缺少 FrontElementsConfig 文件夹。
    /// </summary>
    public const string CodeFrontElementsFolderMissing = "LegacyConvert.FrontElementsFolderMissing";

    /// <summary>
    /// 代码常量 —— 未知的旧版布局文件被跳过。
    /// </summary>
    public const string CodeUnknownLayoutFileSkipped = "LegacyConvert.UnknownLayoutFileSkipped";

    /// <summary>
    /// 代码常量 —— 旧版 Map BP V1 窗口被跳过。
    /// </summary>
    public const string CodeMapBpV1Skipped = "LegacyConvert.MapBpV1Skipped";

    /// <summary>
    /// 代码常量 —— 旧版布局文件过大被跳过。
    /// </summary>
    public const string CodeLayoutFileTooLargeSkipped = "LegacyConvert.LayoutFileTooLargeSkipped";

    /// <summary>
    /// 代码常量 —— 旧版布局文件读取失败。
    /// </summary>
    public const string CodeLayoutFileReadFailed = "LegacyConvert.LayoutFileReadFailed";

    /// <summary>
    /// 代码常量 —— 没有该旧版窗口的转换蓝图。
    /// </summary>
    public const string CodeNoBlueprintForLayout = "LegacyConvert.NoBlueprintForLayout";

    /// <summary>
    /// 代码常量 —— 控件创建失败。
    /// </summary>
    public const string CodeControlCreateFailed = "LegacyConvert.ControlCreateFailed";

    /// <summary>
    /// 代码常量 —— 控件不在显式转换映射表中。
    /// </summary>
    public const string CodeControlNotInBlueprintMap = "LegacyConvert.ControlNotInBlueprintMap";

    /// <summary>
    /// 代码常量 —— 转换后布局存在校验错误。
    /// </summary>
    public const string CodeLayoutValidationError = "LegacyConvert.LayoutValidationError";

    /// <summary>
    /// 代码常量 —— 旧版加赛比分格已合并。
    /// </summary>
    public const string CodeOvertimeScoreCellsAggregated = "LegacyConvert.OvertimeScoreCellsAggregated";

    /// <summary>
    /// 代码常量 —— 旧版锁定图层已合并。
    /// </summary>
    public const string CodeLockOverlayFolded = "LegacyConvert.LockOverlayFolded";

    /// <summary>
    /// 代码常量 —— 锁定图层目标控件不存在。
    /// </summary>
    public const string CodeLockOverlayTargetMissing = "LegacyConvert.LockOverlayTargetMissing";

    /// <summary>
    /// 代码常量 —— BP 概览内容超出原始区域。
    /// </summary>
    public const string CodeBpOverviewOutOfBounds = "LegacyConvert.BpOverviewOutOfBounds";

    /// <summary>
    /// 代码常量 —— 旧版资源未找到。
    /// </summary>
    public const string CodeResourceMissing = "LegacyConvert.ResourceMissing";

    /// <summary>
    /// 代码常量 —— BO3 总比分背景已映射。
    /// </summary>
    public const string CodeBo3GlobalScoreBackgroundMapped = "LegacyConvert.Bo3GlobalScoreBackgroundMapped";

    /// <summary>
    /// 代码常量 —— 旧版锁定图片已合并。
    /// </summary>
    public const string CodeLockImageMapped = "LegacyConvert.LockImageMapped";

    /// <summary>
    /// 代码常量 —— 旧版选中边框图片已合并。
    /// </summary>
    public const string CodePickingBorderImageMapped = "LegacyConvert.PickingBorderImageMapped";

    /// <summary>
    /// 代码常量 —— 旧版文字设置已应用。
    /// </summary>
    public const string CodeTextSettingsApplied = "LegacyConvert.TextSettingsApplied";

    /// <summary>
    /// 代码常量 —— 旧版 MapV2 选中边框已映射。
    /// </summary>
    public const string CodeMapV2PickingBorderMapped = "LegacyConvert.MapV2PickingBorderMapped";

    /// <summary>
    /// 代码常量 —— 旧版资源已复制。
    /// </summary>
    public const string CodeResourceCopied = "LegacyConvert.ResourceCopied";

    /// <summary>
    /// 代码常量 —— 旧版全局比分格已聚合。
    /// </summary>
    public const string CodeGlobalScoreCellsAggregated = "LegacyConvert.GlobalScoreCellsAggregated";

    /// <summary>
    /// 代码常量 —— 单元格间距被近似计算。
    /// </summary>
    public const string CodeIrregularCellSpacingApproximated = "LegacyConvert.IrregularCellSpacingApproximated";

    /// <summary>
    /// 代码常量 —— 旧版控件几何被模糊匹配。
    /// </summary>
    public const string CodeControlGeometryFuzzyMatched = "LegacyConvert.ControlGeometryFuzzyMatched";

    /// <summary>
    /// 代码常量 —— 旧版锁定图层几何被消费。
    /// </summary>
    public const string CodeLockOverlayGeometryConsumed = "LegacyConvert.LockOverlayGeometryConsumed";

    /// <summary>
    /// 代码常量 —— 旧版折叠控件被消费。
    /// </summary>
    public const string CodeFoldedControlConsumed = "LegacyConvert.FoldedControlConsumed";

    /// <summary>
    /// 代码常量 —— 旧版折叠控件无 v3 目标。
    /// </summary>
    public const string CodeFoldedControlNoTarget = "LegacyConvert.FoldedControlNoTarget";

    /// <summary>
    /// 代码常量 —— 旧版字段被忽略。
    /// </summary>
    public const string CodeLegacyFieldIgnored = "LegacyConvert.LegacyFieldIgnored";

    /// <summary>
    /// 代码常量 —— 旧版窗口尺寸与画布默认尺寸不一致。
    /// </summary>
    public const string CodeWindowSizeDiffersFromCanvas = "LegacyConvert.WindowSizeDiffersFromCanvas";

    /// <summary>
    /// 代码常量 —— 旧版 Config.json 过大。
    /// </summary>
    public const string CodeConfigJsonTooLarge = "LegacyConvert.ConfigJsonTooLarge";

    /// <summary>
    /// 代码常量 —— 旧版 Config.json 读取失败。
    /// </summary>
    public const string CodeConfigJsonReadFailed = "LegacyConvert.ConfigJsonReadFailed";

    /// <summary>
    /// 代码常量 —— 旧版文本设置无法读取。
    /// </summary>
    public const string CodeTextSettingsReadFailed = "LegacyConvert.TextSettingsReadFailed";

    /// <summary>
    /// 代码常量 —— 单个旧版文本设置字段格式无效，已跳过该字段。
    /// </summary>
    public const string CodeTextSettingsFieldInvalid = "LegacyConvert.TextSettingsFieldInvalid";

    /// <summary>
    /// 代码常量 —— 旧版窗口设置无法检查。
    /// </summary>
    public const string CodeWindowSettingsInspectFailed = "LegacyConvert.WindowSettingsInspectFailed";

    /// <summary>
    /// 代码常量 —— 折叠控件几何无法独立表示。
    /// </summary>
    public const string CodeFoldedGeometryNotRepresentable = "LegacyConvert.FoldedGeometryNotRepresentable";

    private static readonly Regex ArgPattern = ArgRegex();

    /// <summary>
    /// 根据代码和参数构建本地化消息文本。
    /// 若设置了 <see cref="LocalizeTemplate"/>，则先获取本地化模板，再替换占位符。
    /// 否则使用代码本身，并附加参数信息作为回退消息。
    /// </summary>
    /// <param name="code">消息代码。</param>
    /// <param name="args">参数字典。</param>
    /// <returns>本地化后的消息文本。</returns>
    public static string BuildLocalizedMessage(string code, Dictionary<string, string> args)
    {
        var template = LocalizeTemplate?.Invoke(code);
        if (template is not null)
        {
            if (args.Count == 0)
            {
                return template;
            }

            return ArgPattern.Replace(template, match =>
            {
                var key = match.Groups[1].Value;
                return args.TryGetValue(key, out var value) ? value : match.Value;
            });
        }

        // No localization delegate: use code with appended args for diagnostic clarity.
        if (args.Count == 0)
        {
            return code;
        }

        var sb = new StringBuilder(code);
        sb.Append(" (");
        var first = true;
        foreach (var kvp in args)
        {
            if (!first)
            {
                sb.Append(", ");
            }

            sb.Append(kvp.Key).Append('=').Append(kvp.Value);
            first = false;
        }

        sb.Append(')');
        return sb.ToString();
    }

    /// <summary>
    /// 创建严重级别为 <see cref="FrontedLayoutPackageLegacyConvertMessageSeverity.Info"/> 的消息。
    /// </summary>
    public static FrontedLayoutPackageLegacyConvertMessage Info(string code, Dictionary<string, string>? args = null)
        => CreateMessage(code, FrontedLayoutPackageLegacyConvertMessageSeverity.Info, args);

    /// <summary>
    /// 创建严重级别为 <see cref="FrontedLayoutPackageLegacyConvertMessageSeverity.CompatibilityNote"/> 的消息。
    /// </summary>
    public static FrontedLayoutPackageLegacyConvertMessage Compat(string code, Dictionary<string, string>? args = null)
        => CreateMessage(code, FrontedLayoutPackageLegacyConvertMessageSeverity.CompatibilityNote, args);

    /// <summary>
    /// 创建严重级别为 <see cref="FrontedLayoutPackageLegacyConvertMessageSeverity.Warning"/> 的消息。
    /// </summary>
    public static FrontedLayoutPackageLegacyConvertMessage Warning(string code, Dictionary<string, string>? args = null)
        => CreateMessage(code, FrontedLayoutPackageLegacyConvertMessageSeverity.Warning, args);

    /// <summary>
    /// 创建严重级别为 <see cref="FrontedLayoutPackageLegacyConvertMessageSeverity.Error"/> 的消息。
    /// </summary>
    public static FrontedLayoutPackageLegacyConvertMessage Error(string code, Dictionary<string, string>? args = null)
        => CreateMessage(code, FrontedLayoutPackageLegacyConvertMessageSeverity.Error, args);

    /// <summary>
    /// 从匿名对象创建参数字典。
    /// </summary>
    /// <param name="args">匿名对象，其属性名和值将被转换为字典条目。</param>
    /// <returns>参数字典。</returns>
    public static Dictionary<string, string> Args(object? args)
    {
        if (args is null)
        {
            return [];
        }

        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var prop in args.GetType().GetProperties())
        {
            var value = prop.GetValue(args)?.ToString() ?? string.Empty;
            dict[prop.Name] = value;
        }

        return dict;
    }

    /// <summary>
    /// 对消息列表中的所有消息执行本地化，填充 <see cref="FrontedLayoutPackageLegacyConvertMessage.Message"/> 属性。
    /// </summary>
    /// <param name="messages">消息列表。</param>
    public static void LocalizeAll(List<FrontedLayoutPackageLegacyConvertMessage> messages)
    {
        foreach (var message in messages)
        {
            if (string.IsNullOrEmpty(message.Message))
            {
                message.Message = BuildLocalizedMessage(message.Code, message.Args);
            }
        }
    }

    private static FrontedLayoutPackageLegacyConvertMessage CreateMessage(
        string code,
        FrontedLayoutPackageLegacyConvertMessageSeverity severity,
        Dictionary<string, string>? args)
    {
        var message = new FrontedLayoutPackageLegacyConvertMessage
        {
            Code = code,
            Severity = severity,
            Args = args ?? [],
            Message = BuildLocalizedMessage(code, args ?? [])
        };
        return message;
    }

    [GeneratedRegex(@"\{(\w+)\}", RegexOptions.Compiled)]
    private static partial Regex ArgRegex();
}
