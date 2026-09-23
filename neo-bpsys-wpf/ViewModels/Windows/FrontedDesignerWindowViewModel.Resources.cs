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
/// Fronted Designer 的本地资源引用跟踪与清理业务逻辑。
/// </summary>
public partial class FrontedDesignerWindowViewModel
{
    public void DiscardPendingResourceImports()
    {
        CleanupPendingImportedResources(includeCurrentDocument: false);
    }

    private void RecordPendingImportedResource(
        FrontedLocalResourceStoreResult result,
        string sourceContext,
        bool wasApplied)
    {
        if (!wasApplied || !result.WasNewlyCreated)
        {
            return;
        }

        if (_pendingImportedResources.Any(resource =>
                string.Equals(resource.ResourceUri, result.ResourceUri, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _pendingImportedResources.Add(new PendingImportedResource(
            result.ResourceUri,
            result.PhysicalPath,
            DateTimeOffset.UtcNow,
            sourceContext));
    }

    private void CleanupPendingImportedResources(bool includeCurrentDocument)
    {
        if (_pendingImportedResources.Count == 0 || _localResourceStore is null)
        {
            return;
        }

        var referencedResources = CollectSavedLocalResourceReferences();
        if (includeCurrentDocument && CurrentDocument is not null)
        {
            foreach (var reference in EnumerateLocalResourceReferences(_designConverter.ToConfig(CurrentDocument)))
            {
                referencedResources.Add(reference);
            }
        }

        foreach (var pending in _pendingImportedResources.ToArray())
        {
            if (referencedResources.Contains(pending.ResourceUri))
            {
                _pendingImportedResources.Remove(pending);
                continue;
            }

            try
            {
                if (File.Exists(pending.PhysicalPath))
                {
                    File.Delete(pending.PhysicalPath);
                }

                _pendingImportedResources.Remove(pending);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to cleanup pending fronted designer resource {ResourceUri} from {SourceContext}.",
                    pending.ResourceUri,
                    pending.SourceContext);
            }
        }
    }

    private HashSet<string> CollectSavedLocalResourceReferences()
    {
        var references = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var root = _packageManager?.GetPackageRootFolder() ?? AppConstants.FrontedLayoutPackagesPath;
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return references;
        }

        foreach (var file in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                var json = File.ReadAllText(file);
                if (JsonNode.Parse(json) is not { } node)
                {
                    continue;
                }

                foreach (var reference in EnumerateLocalResourceReferences(node))
                {
                    references.Add(reference);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to scan fronted layout resource references: {Path}", file);
            }
        }

        return references;
    }

    private static IEnumerable<string> EnumerateLocalResourceReferences(FrontedCanvasConfig config)
    {
        var node = JsonSerializer.SerializeToNode(config);
        return node is null ? [] : EnumerateLocalResourceReferences(node);
    }

    private static IEnumerable<string> EnumerateLocalResourceReferences(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (var child in obj)
            {
                if (child.Value is not null)
                {
                    foreach (var reference in EnumerateLocalResourceReferences(child.Value))
                    {
                        yield return reference;
                    }
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                if (child is not null)
                {
                    foreach (var reference in EnumerateLocalResourceReferences(child))
                    {
                        yield return reference;
                    }
                }
            }
        }
        else if (node is JsonValue value
                 && value.TryGetValue<string>(out var text)
                 && text.StartsWith("bpui://local/", StringComparison.OrdinalIgnoreCase))
        {
            yield return text;
        }
    }
}
