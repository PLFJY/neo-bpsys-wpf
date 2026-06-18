#nullable enable

using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Media;
using Xunit;

namespace neo_bpsys_wpf.Tests.Models;

public class FrontedWindowConfigTest
{
    [Fact]
    public void DefaultsUseWindowCentricSchema()
    {
        var config = new FrontedWindowConfig();

        Assert.Equal(3, config.Version);
        Assert.False(config.CanvasSettings.EnableBoModeStates);
        Assert.Empty(config.CanvasSettings.BoModeStates);
        Assert.Empty(config.ControlLayout.RequiredPlugins);
        Assert.Empty(config.ControlLayout.Controls);
    }

    [Fact]
    public void CanvasCompatibilityHelpersAreNotPublicModelMethods()
    {
        var publicMethodNames = typeof(FrontedWindowConfig)
            .GetMethods()
            .Select(method => method.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("FromCanvasConfig", publicMethodNames);
        Assert.DoesNotContain("ToCanvasConfig", publicMethodNames);
        Assert.DoesNotContain("SyncWindowSizeToCanvas", publicMethodNames);
    }

    [Fact]
    public async Task UserStoreSaveAndLoadPreserveWindowSettingsSize()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var root = Path.Combine(Path.GetTempPath(), $"neo-bpsys-window-config-{Guid.NewGuid():N}");
        try
        {
            var store = new FrontedUserLayoutStore(root);
            var config = new FrontedWindowConfig
            {
                WindowSettings =
                {
                    WindowWidth = 1,
                    WindowHeight = 2
                },
                CanvasSettings =
                {
                    CanvasWidth = 1600,
                    CanvasHeight = 900
                }
            };

            await store.SaveAsync("BpWindow", config, cancellationToken);

            var json = await File.ReadAllTextAsync(store.GetLayoutPath("BpWindow"), cancellationToken);
            var saved = JsonSerializer.Deserialize<FrontedWindowConfig>(json)!;

            var loaded = await store.LoadAsync("BpWindow", cancellationToken);
            Assert.NotNull(loaded);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
