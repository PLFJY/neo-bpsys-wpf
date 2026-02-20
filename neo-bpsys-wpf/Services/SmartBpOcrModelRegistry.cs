using System.IO;
using neo_bpsys_wpf.Core;
using Sdcb.PaddleOCR.Models.Online;

namespace neo_bpsys_wpf.Services;

public sealed record SmartBpOcrModelDefinition(
    string Key,
    string DisplayNameKey,
    string DescriptionKey,
    OnlineFullModels OnlineModel);

public static class SmartBpOcrModelRegistry
{
    public static readonly IReadOnlyList<SmartBpOcrModelDefinition> Models =
    [
        new(
            "zh-cn-v5-mobile",
            "SmartBpOcrModelZhCnV5MobileDisplayName",
            "SmartBpOcrModelZhCnV5MobileDescription",
            OnlineFullModels.ChineseV5),
        new(
            "en-v4-mobile",
            "SmartBpOcrModelEnV4MobileDisplayName",
            "SmartBpOcrModelEnV4MobileDescription",
            OnlineFullModels.EnglishV4),
        new(
            "ja-v4-mobile",
            "SmartBpOcrModelJaV4MobileDisplayName",
            "SmartBpOcrModelJaV4MobileDescription",
            OnlineFullModels.JapanV4),
        new(
            "zh-cn-v4",
            "SmartBpOcrModelZhCnV4DisplayName",
            "SmartBpOcrModelZhCnV4Description",
            OnlineFullModels.ChineseV4),
        new(
            "zh-cn-v3-slim",
            "SmartBpOcrModelZhCnV3SlimDisplayName",
            "SmartBpOcrModelZhCnV3SlimDescription",
            OnlineFullModels.ChineseV3Slim)
    ];

    public static string RootDirectory => Path.Combine(AppConstants.AppOutputPath, "OCRModels");

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
