using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3;
using neo_bpsys_wpf.Services.FrontedDesigner;
using neo_bpsys_wpf.Tests.Infrastructure;
using neo_bpsys_wpf.ViewModels.Windows;
using Xunit;

namespace neo_bpsys_wpf.Tests.Models;

/// <summary>
/// Protects the designer operations whose failure would corrupt or lose user edits.
/// </summary>
public sealed class FrontedDesignerBehaviorTest
{
    /// <summary>几何与内容历史交错且切换 BO 状态后，撤销重做不得覆盖其他状态或丢失派生配置字段。</summary>
    [Fact]
    public void MixedGeometryAndContentHistoryPreservesBothBoStates() => WpfTestThread.Run(() =>
    {
        var config = new FrontedCanvasConfig
        {
            EnableBoModeStates = true,
            Controls = { ["Title"] = new TextFrontedControlConfig { Text = "BO5" } },
            BoModeStates =
            {
                ["Bo3"] = new FrontedCanvasStateConfig
                {
                    Controls = { ["Title"] = new TextFrontedControlConfig { Text = "BO3" } }
                }
            }
        };
        var converter = new FrontedLayoutDesignConverter();
        var vm = new FrontedDesignerWindowViewModel { CurrentDocument = converter.FromConfig("TestWindow", "BaseCanvas", config) };
        var textRow = new FrontedPropertyEditorItem { PropertyName = "Text", EditorKind = FrontedPropertyEditorKind.Text };
        vm.SelectDesignItem(vm.CurrentDocument.Controls.Single());
        vm.MoveSelectedDesignItemBy(5, 0);
        Assert.True(vm.ApplyPropertyEdit(textRow, "Edited BO5"));
        vm.SelectedBoModeStateOption = vm.BoModeStateOptions.Single(option => option.State == FrontedCanvasBoModeState.Bo3);
        vm.SelectDesignItem(vm.CurrentDocument.Controls.Single());
        vm.MoveSelectedDesignItemBy(7, 0);
        Assert.True(vm.ApplyPropertyEdit(textRow, "Edited BO3"));
        vm.SelectedBoModeStateOption = vm.BoModeStateOptions.Single(option => option.State == FrontedCanvasBoModeState.Bo5);

        void Verify(string text, double left, string bo3Text, double bo3Left)
        {
            vm.ExecuteScheduledDesignerWorkForTests();
            Assert.Equal(FrontedCanvasBoModeState.Bo5, vm.CurrentDocument.EditingBoModeState);
            var saved = converter.ToConfig(vm.CurrentDocument);
            var root = Assert.IsType<TextFrontedControlConfig>(saved.Controls["Title"]);
            var bo3 = Assert.IsType<TextFrontedControlConfig>(saved.BoModeStates["Bo3"].Controls["Title"]);
            Assert.Equal((text, left, bo3Text, bo3Left), (root.Text, root.Left, bo3.Text, bo3.Left));
            Assert.Null(root.Width);
            Assert.Null(bo3.Width);
        }

        vm.UndoCommand.Execute(null);
        Verify("Edited BO5", 5, "BO3", 7);
        vm.UndoCommand.Execute(null);
        Verify("Edited BO5", 5, "BO3", 0);
        vm.UndoCommand.Execute(null);
        Verify("BO5", 5, "BO3", 0);
        vm.UndoCommand.Execute(null);
        Verify("BO5", 0, "BO3", 0);
        vm.RedoCommand.Execute(null);
        Verify("BO5", 5, "BO3", 0);
        vm.RedoCommand.Execute(null);
        Verify("Edited BO5", 5, "BO3", 0);
        vm.RedoCommand.Execute(null);
        Verify("Edited BO5", 5, "BO3", 7);
        vm.RedoCommand.Execute(null);
        Verify("Edited BO5", 5, "Edited BO3", 7);
    });

    /// <summary>
    /// 图层历史与内容编辑交错时，撤销只能恢复对应操作，并写回操作所属的 BO 状态。
    /// </summary>
    [Fact]
    public void LayerReorderHistoryPreservesContentAndOtherBoState() => WpfTestThread.Run(() =>
    {
        var config = new FrontedCanvasConfig
        {
            EnableBoModeStates = true,
            Controls =
            {
                ["Title"] = new TextFrontedControlConfig { Text = "Original", ZIndex = 0 },
                ["Other"] = new TextFrontedControlConfig { Text = "Other", ZIndex = 2 }
            },
            BoModeStates =
            {
                ["Bo3"] = new FrontedCanvasStateConfig
                {
                    Controls = { ["Title"] = new TextFrontedControlConfig { Text = "BO3", ZIndex = 7 } }
                }
            }
        };
        var viewModel = new FrontedDesignerWindowViewModel
        {
            CurrentDocument = new FrontedLayoutDesignConverter().FromConfig("TestWindow", "BaseCanvas", config)
        };
        var source = viewModel.CurrentDocument.Controls.First(item => item.Name == "Title");
        viewModel.SelectDesignItem(source);
        Assert.True(viewModel.ApplyPropertyEdit(new FrontedPropertyEditorItem
        {
            PropertyName = nameof(TextFrontedControlConfig.Text), EditorKind = FrontedPropertyEditorKind.Text
        }, "Edited"));
        Assert.True(viewModel.CommitLayerDrop(source, 3, null, true));

        viewModel.SelectedBoModeStateOption = viewModel.BoModeStateOptions.Single(option => option.State == FrontedCanvasBoModeState.Bo3);
        viewModel.UndoCommand.Execute(null);
        Assert.Equal(FrontedCanvasBoModeState.Bo3, viewModel.CurrentDocument.EditingBoModeState);
        Assert.Equal(7, viewModel.CurrentDocument.Controls.Single().Config.ZIndex);
        Assert.Equal("BO3", Assert.IsType<TextFrontedControlConfig>(viewModel.CurrentDocument.Controls.Single().Config).Text);
        Assert.Equal(0, viewModel.CurrentDocument.CanvasConfig.Controls["Title"].ZIndex);
        Assert.Equal("Edited", Assert.IsType<TextFrontedControlConfig>(viewModel.CurrentDocument.CanvasConfig.Controls["Title"]).Text);

        viewModel.UndoCommand.Execute(null);
        Assert.Equal("Original", Assert.IsType<TextFrontedControlConfig>(viewModel.CurrentDocument.CanvasConfig.Controls["Title"]).Text);
        viewModel.RedoCommand.Execute(null);
        viewModel.RedoCommand.Execute(null);
        Assert.Equal(3, viewModel.CurrentDocument.CanvasConfig.Controls["Title"].ZIndex);
        Assert.Equal("Edited", Assert.IsType<TextFrontedControlConfig>(viewModel.CurrentDocument.CanvasConfig.Controls["Title"]).Text);
        Assert.Equal("BO3", Assert.IsType<TextFrontedControlConfig>(viewModel.CurrentDocument.Controls.Single().Config).Text);

        viewModel.SelectedBoModeStateOption = viewModel.BoModeStateOptions.Single(option => option.State == FrontedCanvasBoModeState.Bo5);
        var title = viewModel.CurrentDocument.Controls.First(item => item.Name == "Title");
        Assert.True(viewModel.CommitLayerDrop(title, 4, null, true));
        viewModel.UndoCommand.Execute(null);
        Assert.Equal(3, title.Config.ZIndex);
        viewModel.RedoCommand.Execute(null);
        Assert.Equal(4, title.Config.ZIndex);
        viewModel.SelectedBoModeStateOption = viewModel.BoModeStateOptions.Single(option => option.State == FrontedCanvasBoModeState.Bo3);
        var bo3Title = viewModel.CurrentDocument.Controls.Single();
        Assert.True(viewModel.CommitLayerDrop(bo3Title, 9, null, true));
        viewModel.UndoCommand.Execute(null);
        Assert.Equal(7, bo3Title.Config.ZIndex);
        viewModel.RedoCommand.Execute(null);
        Assert.Equal(9, bo3Title.Config.ZIndex);
        Assert.Equal(4, viewModel.CurrentDocument.CanvasConfig.Controls["Title"].ZIndex);
    });

    /// <summary>
    /// Verifies conversion keeps control identity in dictionary keys, not duplicated JSON fields.
    /// </summary>
    [Fact]
    public void DesignDocumentRoundTripUsesControlNamesAsDictionaryKeys()
    {
        var document = CreateDocument(
        [
            new FrontedControlDesignItem
            {
                Name = "StaticTitle",
                Config = new TextFrontedControlConfig { Text = "BP display tool", Left = 10, Top = 20 }
            }
        ]);

        var config = new FrontedLayoutDesignConverter().ToConfig(document);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(config));
        var restored = new FrontedLayoutDesignConverter().FromConfig("TestWindow", "BaseCanvas", config);

        Assert.True(config.Controls.ContainsKey("StaticTitle"));
        Assert.False(json.RootElement.GetProperty("StaticTitle").TryGetProperty("Name", out _));
        Assert.Equal("StaticTitle", Assert.Single(restored.Controls).Name);
    }

    /// <summary>
    /// Verifies a missing plugin remains a warning while an unknown built-in type remains an error.
    /// </summary>
    [Fact]
    public void ValidationDistinguishesMissingPluginsFromUnknownBuiltIns()
    {
        var document = CreateDocument(
        [
            new FrontedControlDesignItem
            {
                Name = "MissingPlugin",
                Config = new PluginFrontedControlConfig { ControlType = "plugin:top.plfjy.missing/TeamCard" }
            },
            new FrontedControlDesignItem
            {
                Name = "Unknown",
                Config = new FrontedControlConfigBase { ControlType = "UnknownBuiltIn" }
            }
        ]);
        var validator = new FrontedLayoutValidator(new FrontedV3ControlRegistry([]));

        var messages = validator.Validate(document);

        Assert.Contains(messages, message => message.Code == "PluginControlMissing" && message.Severity == FrontedLayoutValidationSeverity.Warning);
        Assert.Contains(messages, message => message.Code == "ControlTypeUnknown" && message.Severity == FrontedLayoutValidationSeverity.Error);
    }

    /// <summary>
    /// Verifies paste creates independent state and a new behavior identity, and is undoable.
    /// </summary>
    [Fact]
    public void CopyPasteCreatesIndependentUndoableControl()
    {
        var sourceGuid = Guid.NewGuid();
        var source = Editable("Text9", new TextFrontedControlConfig
        {
            Text = "Copied",
            Left = 10,
            Top = 20,
            BehaviorGuid = sourceGuid
        });
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = CreateDocument([source]) };
        viewModel.SelectDesignItem(source);

        viewModel.CopySelectedControlCommand.Execute(null);
        ((TextFrontedControlConfig)source.Config).Text = "Edited after copy";
        viewModel.PasteControlCommand.Execute(null);

        var pasted = Assert.Single(viewModel.CurrentDocument!.Controls, item => item.Name == "Text10");
        Assert.Equal("Copied", Assert.IsType<TextFrontedControlConfig>(pasted.Config).Text);
        Assert.NotEqual(Guid.Empty, pasted.Config.BehaviorGuid);
        Assert.NotEqual(sourceGuid, pasted.Config.BehaviorGuid);
        Assert.True(viewModel.CanUndo);

        viewModel.UndoCommand.Execute(null);
        Assert.Single(viewModel.CurrentDocument.Controls);
    }

    /// <summary>
    /// Verifies plugin extension data remains intact when copied in the designer.
    /// </summary>
    [Fact]
    public void CopyPastePreservesPluginExtensionData()
    {
        using var extensionJson = JsonDocument.Parse("""{ "Title": "Home", "Nested": { "Enabled": true } }""");
        var source = Editable("TeamCard1", new PluginFrontedControlConfig
        {
            ControlType = "plugin:top.plfjy.missing/TeamCard",
            ExtensionData =
            {
                ["Title"] = extensionJson.RootElement.GetProperty("Title").Clone(),
                ["Nested"] = extensionJson.RootElement.GetProperty("Nested").Clone()
            }
        });
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = CreateDocument([source]) };
        viewModel.SelectDesignItem(source);

        viewModel.CopySelectedControlCommand.Execute(null);
        viewModel.PasteControlCommand.Execute(null);

        var pasted = Assert.IsType<PluginFrontedControlConfig>(
            Assert.Single(viewModel.CurrentDocument!.Controls, item => item.Name == "TeamCard2").Config);
        Assert.Equal("Home", pasted.ExtensionData["Title"].GetString());
        Assert.True(pasted.ExtensionData["Nested"].GetProperty("Enabled").GetBoolean());
    }

    /// <summary>
    /// Verifies property edits participate in undo/redo and a new edit clears redo history.
    /// </summary>
    [Fact]
    public void PropertyEditSupportsUndoRedoAndBranchingHistory()
    {
        var source = Editable("Title", new TextFrontedControlConfig { Text = "Old", Left = 10, Top = 20 });
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = CreateDocument([source]) };
        viewModel.SelectDesignItem(source);
        var row = new FrontedPropertyEditorItem
        {
            PropertyName = nameof(TextFrontedControlConfig.Text),
            EditorKind = FrontedPropertyEditorKind.Text
        };

        Assert.True(viewModel.ApplyPropertyEdit(row, "New"));
        viewModel.UndoCommand.Execute(null);
        Assert.Equal("Old", Assert.IsType<TextFrontedControlConfig>(viewModel.CurrentDocument!.Controls[0].Config).Text);
        viewModel.RedoCommand.Execute(null);
        Assert.Equal("New", Assert.IsType<TextFrontedControlConfig>(viewModel.CurrentDocument.Controls[0].Config).Text);

        viewModel.UndoCommand.Execute(null);
        viewModel.SelectDesignItem(viewModel.CurrentDocument.Controls[0]);
        viewModel.MoveSelectedDesignItemBy(5, 0);
        Assert.False(viewModel.CanRedo);
    }

    /// <summary>
    /// Verifies multi-selection moves every selected control by one shared snapped delta.
    /// </summary>
    [Fact]
    public void MultiSelectionMoveUsesSharedSnappedDelta()
    {
        var first = Editable("First", new TextFrontedControlConfig { Left = 13, Top = 17, Width = 40, Height = 20 });
        var second = Editable("Second", new TextFrontedControlConfig { Left = 28, Top = 32, Width = 40, Height = 20 });
        var viewModel = new FrontedDesignerWindowViewModel
        {
            CurrentDocument = CreateDocument([first, second]),
            SnapEnabled = true
        };
        viewModel.SelectDesignItems([first, second], first);

        viewModel.MoveSelectedDesignItems(
            new Dictionary<FrontedControlDesignItem, FrontedDesignerResolvedBounds>
            {
                [first] = new(13, 17, 40, 20),
                [second] = new(28, 32, 40, 20)
            },
            deltaX: 5,
            deltaY: 5,
            renderPreview: false);

        Assert.Equal((20D, 20D), (first.Config.Left, first.Config.Top));
        Assert.Equal((35D, 35D), (second.Config.Left, second.Config.Top));
        Assert.Equal([first, second], viewModel.SelectedDesignItems);
    }

    private static FrontedControlDesignItem Editable(string name, FrontedControlConfigBase config) =>
        new()
        {
            Name = name,
            Config = config,
            IsSelectableInEditor = true,
            IsEditableInEditor = true
        };

    private static FrontedCanvasDesignDocument CreateDocument(IList<FrontedControlDesignItem> controls) =>
        new()
        {
            WindowTypeName = "TestWindow",
            CanvasName = "BaseCanvas",
            CanvasConfig = new FrontedCanvasConfig
            {
                Version = 3,
                CanvasWidth = 1440,
                CanvasHeight = 810
            },
            Controls = new(controls)
        };
}
