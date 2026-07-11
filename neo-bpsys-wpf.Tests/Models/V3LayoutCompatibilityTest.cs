#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using Xunit;

namespace neo_bpsys_wpf.Tests.Models;

/// <summary>
/// Locks compatibility of repository-owned window-centric v3 layout fixtures.
/// </summary>
public sealed class V3LayoutCompatibilityTest
{
    [Fact]
    public void CanvasLayoutV3_ShouldDeserializeExistingLayoutJson()
    {
        foreach (var path in GetLayoutPaths())
        {
            var layout = JsonSerializer.Deserialize<FrontedWindowConfig>(File.ReadAllText(path));
            Assert.NotNull(layout);
            Assert.Equal(3, layout!.Version);
        }
    }

    [Fact]
    public void CanvasLayoutV3_ShouldRoundTripExistingLayoutSemantically()
    {
        foreach (var path in GetLayoutPaths())
        {
            var original = JsonNode.Parse(File.ReadAllText(path));
            var layout = JsonSerializer.Deserialize<FrontedWindowConfig>(original!.ToJsonString());
            var roundTripped = JsonNode.Parse(JsonSerializer.Serialize(layout));

            Assert.True(
                JsonNode.DeepEquals(original, roundTripped),
                $"V3 layout semantic round-trip changed '{Path.GetFileName(path)}'.");
        }
    }

    [Fact]
    public void CanvasLayoutV3_ShouldPreserveLocalizedTextKeys()
    {
        foreach (var path in GetLayoutPaths())
        {
            var layout = JsonSerializer.Deserialize<FrontedWindowConfig>(File.ReadAllText(path))!;
            var controls = layout.ControlLayout.Controls.Values
                .Concat(layout.CanvasSettings.BoModeStates.Values.SelectMany(state => state.Controls.Values));

            foreach (var localized in controls.OfType<LocalizedTextControlConfig>())
            {
                Assert.False(string.IsNullOrWhiteSpace(localized.LocalizationKey), path);
                Assert.Null(typeof(LocalizedTextControlConfig).GetProperty("LocalizationDictionary"));
                Assert.Null(typeof(LocalizedTextControlConfig).GetProperty("LocalizationAssembly"));
            }
        }
    }

    private static IReadOnlyList<string> GetLayoutPaths([CallerFilePath] string sourceFilePath = "")
    {
        var root = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(sourceFilePath)!, "..", "..", "neo-bpsys-wpf", "Resources", "FrontedLayouts"));
        return new[] { "BpWindow.json", "GameDataWindow.json", "ScoreGlobalWindow.json" }
            .Select(fileName => Path.Combine(root, fileName))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }
}
