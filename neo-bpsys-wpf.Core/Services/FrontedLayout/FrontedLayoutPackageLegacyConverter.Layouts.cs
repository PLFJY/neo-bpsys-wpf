#pragma warning disable CS1591

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Converters;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using neo_bpsys_wpf.Core.Models.Legacy;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows.Media;
using static neo_bpsys_wpf.Core.Services.FrontedLayout.LegacyConvertMessageHelper;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 旧版前台布局包转换器的Layouts逻辑。
/// </summary>
public sealed partial class FrontedLayoutPackageLegacyConverter
{
    private async Task<int> ConvertFrontElementsConfigsAsync(
        ILegacyFrontendInputSource source,
        string stagingRoot,
        FrontedLayoutPackageManifest manifest,
        ResourceConvertState resourceState,
        IReadOnlyDictionary<string, string> configValueMap,
        LegacySettings? legacySettings,
        IReadOnlySet<string> legacyPropertySet,
        ICollection<FrontedLayoutPackageLegacyConvertMessage> messages,
        CancellationToken cancellationToken)
    {
        var convertedCount = 0;
        var files = source.EnumerateLegacyLayoutFiles().ToArray();
        if (files.Length == 0 && source is LegacyBpuiDirectoryInputSource)
        {
            messages.Add(Error(CodeFrontElementsFolderMissing));
            return 0;
        }

        if (source is LegacyLocalAppDataInputSource)
        {
            var existing = files.Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var expected in LegacyLayoutFileMap.Keys.Where(file => !existing.Contains(file)))
            {
                messages.Add(Warning(CodeLayoutFileReadFailed,
                    Args(new { FileName = expected, Reason = "File is missing." })));
            }
        }

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileName = Path.GetFileName(file);
            if (!TryMapLegacyLayoutFile(fileName, out var mapping))
            {
                messages.Add(Warning(CodeUnknownLayoutFileSkipped,
                    Args(new { FileName = fileName })));
                continue;
            }

            if (!mapping.IsSupported)
            {
                messages.Add(Compat(CodeMapBpV1Skipped,
                    Args(new { SourceWindow = "WidgetsWindow", SourceCanvas = "MapBpCanvas" })));
                continue;
            }

            var legacyPositions = ReadLegacyPositions(source, file, messages);
            if (legacyPositions is null)
            {
                continue;
            }

            var windowConfig = CreateLegacyWindowConfig(mapping);
            var config = FrontedWindowConfigCanvasAdapter.ToCanvasConfig(windowConfig);
            BuildLegacyBlueprintControls(
                mapping,
                config,
                legacyPositions,
                messages);
            ApplyFrontendConfigValues(config, mapping, configValueMap, messages);
            ApplyLegacyTextStyleOverrides(config, mapping, legacySettings, messages);

            RewriteKnownResourceStrings(config, resourceState);
            config.Version = 3;
            ApplyCanvasConfig(windowConfig, config);
            ApplyLegacyWindowSettings(windowConfig, mapping, legacySettings, legacyPropertySet, messages);

            var validationMessages = _validator.Validate(
                mapping.TargetWindow!,
                FrontedLayoutConstants.BaseCanvasName,
                FrontedWindowConfigCanvasAdapter.ToCanvasConfig(windowConfig));
            var validationErrors = validationMessages
                .Where(message => message.Severity == Models.FrontedLayout.Designer.FrontedLayoutValidationSeverity.Error)
                .ToArray();
            if (validationErrors.Length > 0)
            {
                messages.Add(Warning(CodeLayoutValidationError,
                    Args(new
                    {
                        TargetWindow = mapping.TargetWindow,
                        CanvasName = FrontedLayoutConstants.BaseCanvasName,
                        Details = string.Join("; ", validationErrors.Select(error => error.Message))
                    })));
                continue;
            }

            var relativePath = mapping.TargetLayoutPath;
            var targetPath = Path.Combine(stagingRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            var json = JsonSerializer.Serialize(windowConfig, _jsonOptions);
            await File.WriteAllTextAsync(targetPath, json, cancellationToken);

            manifest.Content.Layouts.Add(new FrontedLayoutPackageLayoutEntry
            {
                Window = mapping.TargetWindow!,
                Path = relativePath
            });
            convertedCount++;
        }

        return convertedCount;
    }

    private static FrontedWindowConfig CreateLegacyWindowConfig(LegacyLayoutMapping mapping)
    {
        var defaults = GetLegacyWindowDefaults(mapping);
        return new FrontedWindowConfig
        {
            Version = 3,
            WindowSettings = new FrontedWindowSettings
            {
                WindowWidth = defaults.WindowWidth,
                WindowHeight = defaults.WindowHeight,
                AllowsTransparency = true,
                BackgroundColor = "#00000000",
                Topmost = false,
                ViewboxStretch = Stretch.Fill
            },
            CanvasSettings = new FrontedCanvasSettings
            {
                CanvasWidth = defaults.CanvasWidth,
                CanvasHeight = defaults.CanvasHeight,
                BackgroundImage = defaults.BackgroundImage,
                EnableBoModeStates = false,
                BoModeStates = []
            },
            ControlLayout = new FrontedControlLayout
            {
                RequiredPlugins = [],
                Controls = []
            }
        };
    }

    private static Dictionary<string, ElementInfo>? ReadLegacyPositions(
        ILegacyFrontendInputSource source,
        string legacyFile,
        ICollection<FrontedLayoutPackageLegacyConvertMessage> messages)
    {
        try
        {
            using var stream = source.OpenLegacyLayoutFile(legacyFile);
            if (stream.Length > FrontedLayoutLimits.MaxLegacyConfigBytes)
            {
                messages.Add(Warning(CodeLayoutFileTooLargeSkipped,
                    Args(new { FileName = Path.GetFileName(legacyFile) })));
                return null;
            }

            return JsonSerializer.Deserialize<Dictionary<string, ElementInfo>>(
                stream,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    MaxDepth = FrontedLayoutLimits.MaxJsonDepth
                });
        }
        catch (Exception ex)
        {
            messages.Add(Warning(CodeLayoutFileReadFailed,
                Args(new { FileName = Path.GetFileName(legacyFile), Reason = ex.Message })));
            return null;
        }
    }

    private void BuildLegacyBlueprintControls(
        LegacyLayoutMapping mapping,
        FrontedCanvasConfig config,
        IReadOnlyDictionary<string, ElementInfo> legacyPositions,
        ICollection<FrontedLayoutPackageLegacyConvertMessage> messages)
    {
        if (legacyPositions is null)
        {
            return;
        }

        var key = new LegacyLayoutKey(mapping.SourceWindow, mapping.SourceCanvas);
        if (!LegacyControlBlueprints.TryGetValue(key, out var blueprints))
        {
            messages.Add(Warning(CodeNoBlueprintForLayout,
                Args(new { SourceWindow = mapping.SourceWindow, SourceCanvas = mapping.SourceCanvas })));
            return;
        }

        foreach (var blueprint in blueprints)
        {
            if (blueprint.Status is not LegacyControlBlueprintStatus.Mapped
                and not LegacyControlBlueprintStatus.Aggregated)
            {
                continue;
            }

            if (!blueprint.Required && !legacyPositions.ContainsKey(blueprint.LegacyName))
            {
                continue;
            }

            var control = CreateBlueprintControl(blueprint);
            if (control is null)
            {
                messages.Add(Warning(CodeControlCreateFailed,
                    Args(new
                    {
                        SourceWindow = mapping.SourceWindow,
                        SourceCanvas = mapping.SourceCanvas,
                        LegacyName = blueprint.LegacyName,
                        TargetName = blueprint.TargetName
                    })));
                continue;
            }

            config.Controls[blueprint.TargetName] = control;
        }

        var consumedControls = new HashSet<string>(StringComparer.Ordinal);
        ApplyScoreGlobalAggregateGeometry(mapping.SourceWindow, mapping.SourceCanvas, config, legacyPositions, consumedControls, messages);
        ConsumeExplicitFoldedGeometry(mapping.SourceWindow, mapping.SourceCanvas, blueprints, config, legacyPositions, consumedControls, messages);
        var blueprintsByLegacyName = blueprints
            .GroupBy(blueprint => blueprint.LegacyName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);

        foreach (var (controlName, legacy) in legacyPositions)
        {
            if (consumedControls.Contains(controlName))
            {
                continue;
            }

            if (!blueprintsByLegacyName.TryGetValue(controlName, out var mappedBlueprints))
            {
                if (TryResolveBlueprintLegacyNameAlias(mapping.SourceWindow, mapping.SourceCanvas, controlName, out var aliasName)
                    && blueprintsByLegacyName.TryGetValue(aliasName, out mappedBlueprints))
                {
                    messages.Add(Info(CodeControlGeometryFuzzyMatched,
                        Args(new
                        {
                            SourceWindow = mapping.SourceWindow,
                            SourceCanvas = mapping.SourceCanvas,
                            LegacyName = controlName,
                            ResolvedLegacyName = aliasName
                        })));
                }
                else
                {
                    throw new InvalidDataException(
                        $"Legacy control geometry is not listed in the explicit legacy blueprint map: {mapping.SourceWindow}/{mapping.SourceCanvas}/{controlName}");
                }
            }

            foreach (var blueprint in mappedBlueprints)
            {
                if (blueprint.Status is LegacyControlBlueprintStatus.Folded
                    or LegacyControlBlueprintStatus.Aggregated
                    or LegacyControlBlueprintStatus.Unsupported
                    or LegacyControlBlueprintStatus.RemovedWithReason)
                {
                    continue;
                }

                if (config.Controls.TryGetValue(blueprint.TargetName, out var control))
                {
                    ApplyGeometry(control, legacy);
                }
            }
        }

        AddBoundsDiagnostics(mapping, legacyPositions.Values, messages);
    }

    private FrontedControlConfigBase? CreateBlueprintControl(
        LegacyControlBlueprint blueprint)
    {
        var created = CreateDefaultControl(blueprint);
        if (created is null)
        {
            return null;
        }

        ApplyBlueprintDefaults(blueprint, created);
        created.Visibility = FrontedControlVisibility.Visible;
        return created;
    }

}

#pragma warning restore CS1591
