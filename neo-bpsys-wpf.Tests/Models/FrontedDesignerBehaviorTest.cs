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
using neo_bpsys_wpf.ViewModels.Windows;
using Xunit;

namespace neo_bpsys_wpf.Tests.Models;

/// <summary>
/// Protects the designer operations whose failure would corrupt or lose user edits.
/// </summary>
public sealed class FrontedDesignerBehaviorTest
{
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
