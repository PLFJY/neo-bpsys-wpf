using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.PaddleRuntime;
using OpenCvSharp;
using Sdcb.PaddleInference;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models;
using Sdcb.PaddleOCR.Models.Shared;
using System.Collections;
using System.Formats.Tar;
using System.IO;
using System.Text;
using System.Threading;
using System.Security.Cryptography;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// PaddleOCR 提供者的本地化与元数据辅助逻辑。
/// </summary>
public sealed partial class PaddleOcrProvider
{
    /// <summary>
    /// 读取 SmartBP 模块本地化文本。
    /// </summary>
    /// <param name="key">资源 key。</param>
    /// <returns>本地化文本。</returns>
    private static string L(string key) => I18nHelper.GetLocalizedString(key);

    /// <summary>
    /// 读取并格式化 SmartBP 模块本地化文本。
    /// </summary>
    /// <param name="key">资源 key。</param>
    /// <param name="args">格式化参数。</param>
    /// <returns>格式化后的本地化文本。</returns>
    private static string Lf(string key, params object?[] args) =>
        string.Format(I18nHelper.GetLocalizedString(key), args);

    /// <summary>
    /// 从 PaddleOCR 模型元数据中选择下载地址。
    /// </summary>
    /// <param name="onlineModel">Sdcb 模型元数据对象。</param>
    /// <param name="errorMessage">元数据缺失时抛出的本地化错误。</param>
    /// <returns>模型下载地址。</returns>
    /// <exception cref="InvalidOperationException">模型元数据缺少可用下载地址时抛出。</exception>
    private Uri PickModelSourceUri(object? onlineModel, string errorMessage)
    {
        if (onlineModel is null)
        {
            _logger.LogError("PickModelSourceUri: onlineModel is null. Error: {ErrorMessage}", errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        var modelType = onlineModel.GetType();

        // 兼容旧版：Uri
        var legacyUriValue = modelType.GetProperty("Uri")?.GetValue(onlineModel);
        if (legacyUriValue is Uri legacyUri)
        {
            return legacyUri;
        }

        if (legacyUriValue is string legacyUrl &&
            Uri.TryCreate(legacyUrl, UriKind.Absolute, out var parsedLegacyUri))
        {
            return parsedLegacyUri;
        }

        // 新版：Sources
        var sourcesValue = modelType.GetProperty("Sources")?.GetValue(onlineModel);
        if (sourcesValue is not IEnumerable sources)
        {
            _logger.LogError("PickModelSourceUri: Sources property is not IEnumerable. Error: {ErrorMessage}", errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        var candidates = sources
            .Cast<object>()
            .Select(source =>
            {
                var sourceType = source.GetType();

                var uriValue =
                    sourceType.GetProperty("ArchiveUri")?.GetValue(source) ??
                    sourceType.GetProperty("Uri")?.GetValue(source) ??
                    sourceType.GetProperty("Url")?.GetValue(source);

                if (uriValue is Uri uri)
                {
                    return uri;
                }

                if (uriValue is string url &&
                    Uri.TryCreate(url, UriKind.Absolute, out var parsedUri))
                {
                    return parsedUri;
                }

                var description = sourceType.GetProperty("Description")?.GetValue(source)?.ToString();

                return Uri.TryCreate(description, UriKind.Absolute, out var descriptionUri)
                    ? descriptionUri
                    : null;
            })
            .Where(uri => uri is not null)
            .Cast<Uri>()
            .ToList();

        var selected = candidates
            .Where(uri => uri.AbsoluteUri.EndsWith(".tar", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(uri => uri.Host.Contains("bcebos.com", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();

        if (selected == null)
        {
            _logger.LogError("PickModelSourceUri: no valid source URI found. Error: {ErrorMessage}", errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        return selected;
    }
}
