using neo_bpsys_wpf.Core.Abstractions.Services;
using System.IO;

namespace neo_bpsys_wpf.Services.SmartBpModule;

/// <summary>
/// Provides the SmartBP OCR model root from the loaded module state.
/// </summary>
public sealed class SmartBpOcrModelPathProvider : ISmartBpOcrModelPathProvider
{
    private readonly ISmartBpModuleStorageProvider _storageProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmartBpOcrModelPathProvider"/> class.
    /// </summary>
    /// <param name="moduleManager">Module manager.</param>
    public SmartBpOcrModelPathProvider(ISmartBpModuleStorageProvider storageProvider)
    {
        _storageProvider = storageProvider;
    }

    /// <inheritdoc />
    public string RootDirectory => _storageProvider.OcrModelsRoot;
}
