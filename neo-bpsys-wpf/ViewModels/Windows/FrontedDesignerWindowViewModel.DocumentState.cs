using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Messages;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer.V3;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Parts;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.StyleTransfer;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Geometry;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Parts;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.StyleTransfer;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Services.FrontedDesigner;
using neo_bpsys_wpf.ViewModels.FrontedDesigner;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace neo_bpsys_wpf.ViewModels.Windows;

/// <summary>
/// Fronted Designer 的设计文档重建与配置克隆业务逻辑。
/// </summary>
public partial class FrontedDesignerWindowViewModel
{
    private void RebuildDocumentFromConfig(
        FrontedCanvasConfig config,
        FrontedCanvasBoModeState editingState,
        bool preserveDirty,
        string? selectedControlName)
    {
        if (CurrentDocument is null)
        {
            return;
        }

        var document = _designConverter.FromConfig(
            CurrentDocument.WindowTypeName,
            CurrentDocument.CanvasName,
            config,
            editingState);
        document.IsDirty = preserveDirty || CurrentDocument.IsDirty;

        _preserveUndoRedoDuringDocumentSwap = true;
        try
        {
            CurrentDocument = document;
        }
        finally
        {
            _preserveUndoRedoDuringDocumentSwap = false;
        }

        SelectDesignItem(document.Controls.FirstOrDefault(control =>
            string.Equals(control.Name, selectedControlName, StringComparison.Ordinal)));
        RefreshCanvasPropertyBuffers();
        RefreshDirtyState();
        ValidateCurrentDocument();
        RequestPreviewRenderCurrentDocument();
    }

    private static T DeepClone<T>(T value)
    {
        var json = JsonSerializer.Serialize(value);
        var result = JsonSerializer.Deserialize<T>(json);
        if (result is null)
        {
            StaticLogger?.LogError("Failed to clone fronted layout state.");
            throw new InvalidOperationException("Failed to clone fronted layout state.");
        }

        return result;
    }

    private FrontedWindowConfig CreateConfigFromCurrentDocument()
    {
        if (CurrentDocument is null)
        {
            return new FrontedWindowConfig
            {
                WindowSettings = CloneWindowSettings(_currentWindowSettings)
            };
        }

        var canvasConfig = _designConverter.ToConfig(CurrentDocument);
        canvasConfig.Version = 3;
        var windowConfig = FrontedWindowConfigCanvasAdapter.FromCanvasConfig(canvasConfig);
        windowConfig.WindowSettings = CloneWindowSettings(_currentWindowSettings);
        return windowConfig;
    }

    private static FrontedWindowSettings CloneWindowSettings(FrontedWindowSettings settings)
    {
        return new FrontedWindowSettings
        {
            WindowWidth = settings.WindowWidth,
            WindowHeight = settings.WindowHeight,
            WindowLeft = settings.WindowLeft,
            WindowTop = settings.WindowTop,
            AllowsTransparency = settings.AllowsTransparency,
            BackgroundColor = settings.BackgroundColor,
            Topmost = settings.Topmost,
            ViewboxStretch = settings.ViewboxStretch
        };
    }

    private static Dictionary<string, FrontedControlConfigBase> CloneControls(
        IReadOnlyDictionary<string, FrontedControlConfigBase> controls)
    {
        var cloned = new Dictionary<string, FrontedControlConfigBase>(StringComparer.Ordinal);
        foreach (var (name, control) in controls)
        {
            var json = JsonSerializer.Serialize(control, control.GetType());
            var deserialized = (FrontedControlConfigBase?)JsonSerializer.Deserialize(json, control.GetType());
            if (deserialized is null)
            {
                StaticLogger?.LogError("Failed to clone fronted control config.");
                throw new InvalidOperationException("Failed to clone fronted control config.");
            }

            cloned[name] = deserialized;
        }

        return cloned;
    }
}
