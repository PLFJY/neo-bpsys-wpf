using neo_bpsys_wpf.Core.Abstractions.Services;
using System.IO;

namespace neo_bpsys_wpf.Services.SmartBpModule;

/// <summary>
/// 从已加载的模块状态提供 SmartBP OCR 模型根目录。
/// </summary>
public sealed class SmartBpOcrModelPathProvider : ISmartBpOcrModelPathProvider
{
    private readonly ISmartBpModuleStorageProvider _storageProvider;

    /// <summary>
    /// 初始化 <see cref="SmartBpOcrModelPathProvider"/> 类的新实例。
    /// </summary>
    /// <param name="moduleManager">模块管理器。</param>
    public SmartBpOcrModelPathProvider(ISmartBpModuleStorageProvider storageProvider)
    {
        _storageProvider = storageProvider;
    }

    /// <inheritdoc />
    public string RootDirectory => _storageProvider.OcrModelsRoot;
}
