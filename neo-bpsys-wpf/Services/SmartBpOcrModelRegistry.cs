using System.IO;
using neo_bpsys_wpf.Core;
using Sdcb.PaddleOCR.Models.Online;

namespace neo_bpsys_wpf.Services;

public sealed record SmartBpOcrModelDefinition(
    string Key,
    string DisplayName,
    string Description,
    OnlineFullModels OnlineModel);

public static class SmartBpOcrModelRegistry
{
    public static readonly IReadOnlyList<SmartBpOcrModelDefinition> Models =
    [
        new(
            "zh-cn-v5-mobile",
            "中文 V5 Mobile（推荐）",
            "精度与速度平衡，优先建议。",
            OnlineFullModels.ChineseV5),
        new(
            "en-v4-mobile",
            "English V4 Mobile",
            "英文场景优化，适合英文与数字识别。",
            OnlineFullModels.EnglishV4),
        new(
            "ja-v4-mobile",
            "Japanese V4 Mobile",
            "日文场景优化，适合日文与数字识别。",
            OnlineFullModels.JapanV4),
        new(
            "zh-cn-v4",
            "中文 V4",
            "兼容性稳定，部署体积适中。",
            OnlineFullModels.ChineseV4),
        new(
            "zh-cn-v3-slim",
            "中文 V3 Slim（轻量）",
            "体积更小，适合低配置设备。",
            OnlineFullModels.ChineseV3Slim)
    ];

    public static string RootDirectory => Path.Combine(AppConstants.AppOutputPath, "OCRModels");

    public static string ActiveModelConfigDirectory => Path.Combine(AppConstants.AppDataPath, "OCRModels");

    public static string ActiveModelFilePath => Path.Combine(ActiveModelConfigDirectory, "active-model.txt");

    public static string LegacyActiveModelFilePath => Path.Combine(RootDirectory, "active-model.txt");

    public static bool TryGet(string modelKey, out SmartBpOcrModelDefinition definition)
    {
        var selected = Models.FirstOrDefault(m => m.Key == modelKey);
        if (selected is null)
        {
            definition = default!;
            return false;
        }

        definition = selected;
        return true;
    }

    public static string GetModelDirectory(string modelKey) => Path.Combine(RootDirectory, modelKey);

    public static string GetDetDirectory(string modelKey) => Path.Combine(GetModelDirectory(modelKey), "det");

    public static string GetClsDirectory(string modelKey) => Path.Combine(GetModelDirectory(modelKey), "cls");

    public static string GetRecDirectory(string modelKey) => Path.Combine(GetModelDirectory(modelKey), "rec");

    public static string GetRecDictPath(string modelKey) => Path.Combine(GetModelDirectory(modelKey), "dict.txt");

    public static bool IsModelInstalled(string modelKey) =>
        IsModelComponentReady(GetDetDirectory(modelKey)) &&
        IsModelComponentReady(GetClsDirectory(modelKey)) &&
        IsModelComponentReady(GetRecDirectory(modelKey));

    private static bool IsModelComponentReady(string modelDirectory) =>
        File.Exists(Path.Combine(modelDirectory, "inference.pdiparams")) &&
        (File.Exists(Path.Combine(modelDirectory, "inference.pdmodel")) ||
         File.Exists(Path.Combine(modelDirectory, "inference.json")));
}
