using System.IO;
using neo_bpsys_wpf.Core;
using Sdcb.PaddleOCR.Models.Online;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 定义 SmartBp 可用 OCR 模型的元数据。
/// </summary>
/// <param name="Key">模型唯一键。</param>
/// <param name="DisplayNameKey">显示名称本地化键。</param>
/// <param name="DescriptionKey">描述本地化键。</param>
/// <param name="OnlineModel">在线模型定义。</param>
public sealed record SmartBpOcrModelDefinition(
    string Key,
    string DisplayNameKey,
    string DescriptionKey,
    OnlineFullModels OnlineModel);

/// <summary>
/// SmartBp OCR 模型注册表。
/// 负责维护模型列表与本地文件路径解析逻辑。
/// </summary>
public static class SmartBpOcrModelRegistry
{
    /// <summary>
    /// 已注册的 OCR 模型列表。
    /// </summary>
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

    /// <summary>
    /// OCR 模型根目录。
    /// </summary>
    public static string RootDirectory => Path.Combine(AppConstants.AppOutputPath, "OCRModels");

    /// <summary>
    /// 按模型键获取模型定义。
    /// </summary>
    /// <param name="modelKey">模型键。</param>
    /// <param name="definition">输出的模型定义。</param>
    /// <returns>找到返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
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

    /// <summary>
    /// 获取模型目录。
    /// </summary>
    /// <param name="modelKey">模型键。</param>
    /// <returns>模型目录路径。</returns>
    public static string GetModelDirectory(string modelKey) => Path.Combine(RootDirectory, modelKey);

    /// <summary>
    /// 获取检测模型目录。
    /// </summary>
    /// <param name="modelKey">模型键。</param>
    /// <returns>检测模型目录路径。</returns>
    public static string GetDetDirectory(string modelKey) => Path.Combine(GetModelDirectory(modelKey), "det");

    /// <summary>
    /// 获取方向分类模型目录。
    /// </summary>
    /// <param name="modelKey">模型键。</param>
    /// <returns>分类模型目录路径。</returns>
    public static string GetClsDirectory(string modelKey) => Path.Combine(GetModelDirectory(modelKey), "cls");

    /// <summary>
    /// 获取识别模型目录。
    /// </summary>
    /// <param name="modelKey">模型键。</param>
    /// <returns>识别模型目录路径。</returns>
    public static string GetRecDirectory(string modelKey) => Path.Combine(GetModelDirectory(modelKey), "rec");

    /// <summary>
    /// 获取识别模型字典文件路径。
    /// </summary>
    /// <param name="modelKey">模型键。</param>
    /// <returns>字典文件路径。</returns>
    public static string GetRecDictPath(string modelKey) => Path.Combine(GetModelDirectory(modelKey), "dict.txt");

    /// <summary>
    /// 判断模型文件是否完整可用。
    /// </summary>
    /// <param name="modelKey">模型键。</param>
    /// <returns>完整可用返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    public static bool IsModelInstalled(string modelKey) =>
        IsModelComponentReady(GetDetDirectory(modelKey)) &&
        IsModelComponentReady(GetClsDirectory(modelKey)) &&
        IsModelComponentReady(GetRecDirectory(modelKey));

    /// <summary>
    /// 判断单个模型组件目录是否包含可用推理文件。
    /// </summary>
    /// <param name="modelDirectory">组件目录。</param>
    /// <returns>可用返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    private static bool IsModelComponentReady(string modelDirectory) =>
        File.Exists(Path.Combine(modelDirectory, "inference.pdiparams")) &&
        (File.Exists(Path.Combine(modelDirectory, "inference.pdmodel")) ||
         File.Exists(Path.Combine(modelDirectory, "inference.json")));
}
