#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Properties;
using neo_bpsys_wpf.ViewModels.Windows;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// Task 5 SubTask 5.6 测试：覆盖 Designer ViewModel 的"应用到同类型控件"命令
/// (<see cref="FrontedDesignerWindowViewModel.ApplyAppearanceToSameTypeCommand"/>)。
/// </summary>
/// <remarks>
/// <para>
/// 这些测试验证 Task 5 的核心契约：
/// <list type="bullet">
/// <item>命令仅对声明了 <c>SupportsPeerStyleTransfer</c> 的控件可见（<see cref="FrontedDesignerWindowViewModel.CanShowPeerStyleTransferButton"/>）。</item>
/// <item>命令仅在同窗口存在相同 <see cref="FrontedControlConfigBase.ControlType"/> 的其他控件时启用。</item>
/// <item>执行命令时，<see cref="FrontedV3PropertySemantic.Appearance"/> 语义的属性被传播到 peer。</item>
/// <item>位置（<c>Left</c>/<c>Top</c>）与尺寸（<c>Width</c>/<c>Height</c>）等非 Appearance 语义属性不被传播。</item>
/// <item>数据身份字段（<c>MapKey</c>）不被传播。</item>
/// </list>
/// </para>
/// <para>
/// 这些测试通过设计时构造函数构造 ViewModel。需要 <c>SupportsPeerStyleTransfer</c>
/// 的场景使用 <c>MapV2DisplayControlConfig</c> + 一个真实 <see cref="IFrontedV3ControlRegistry"/>
/// （通过 <see cref="FrontedDesignerWindowViewModel(IFrontedV3ControlRegistry)"/> 测试构造函数注入）。
/// <c>MapV2Display</c> 是目前唯一声明 <c>SupportsPeerStyleTransfer = true</c> 的内置控件。
/// </para>
/// <para>
/// 测试不涉及 WPF 视觉树，因此不需要
/// <see cref="neo_bpsys_wpf.Tests.Infrastructure.WpfTestThread"/>。
/// </para>
/// </remarks>
public class FrontedDesignerStyleTransferUITest
{
    // -------------------------------------------------------------------
    // 1. ApplyAppearanceToSameType_TransfersOnlyAppearance
    // -------------------------------------------------------------------

    /// <summary>
    /// 执行 <see cref="FrontedDesignerWindowViewModel.ApplyAppearanceToSameTypeCommand"/>
    /// 必须将源控件的 Appearance 语义属性（<c>MapNameColor</c>、<c>TeamNameColor</c>、<c>CampNameColor</c>）传播到 peer 控件；
    /// 而 <c>Left</c>、<c>Top</c>、<c>Width</c>、<c>Height</c>、<c>MapKey</c> 不被传播。
    /// </summary>
    [Fact]
    public void ApplyAppearanceToSameType_TransfersOnlyAppearance()
    {
        var source = new FrontedControlDesignItem
        {
            Name = "MapV2_1",
            IsSelectableInEditor = true,
            IsEditableInEditor = true,
            Config = new MapV2DisplayControlConfig
            {
                MapKey = "ArmsFactory",
                MapNameColor = "#FF0000",
                MapNameFontSize = 24,
                TeamNameColor = "#00FF00",
                CampNameColor = "#0000FF",
                Left = 10,
                Top = 20,
                Width = 100,
                Height = 30
            }
        };
        var peer = new FrontedControlDesignItem
        {
            Name = "MapV2_2",
            IsSelectableInEditor = true,
            IsEditableInEditor = true,
            Config = new MapV2DisplayControlConfig
            {
                MapKey = "AnotherMap",
                MapNameColor = "#111111",
                MapNameFontSize = 12,
                TeamNameColor = "#222222",
                CampNameColor = "#333333",
                Left = 200,
                Top = 300,
                Width = 200,
                Height = 60
            }
        };
        var document = CreateDocument([source, peer]);
        var viewModel = new FrontedDesignerWindowViewModel(CreateMapV2Registry())
        {
            CurrentDocument = document
        };
        viewModel.SelectDesignItem(source);

        Assert.True(viewModel.HasSameTypePeers);
        Assert.True(viewModel.CanShowPeerStyleTransferButton);
        Assert.True(viewModel.ApplyAppearanceToSameTypeCommand.CanExecute(null));

        viewModel.ApplyAppearanceToSameTypeCommand.Execute(null);

        var peerConfig = (MapV2DisplayControlConfig)peer.Config;
        // Appearance 语义属性被传播
        Assert.Equal("#FF0000", peerConfig.MapNameColor);
        Assert.Equal(24, peerConfig.MapNameFontSize);
        Assert.Equal("#00FF00", peerConfig.TeamNameColor);
        Assert.Equal("#0000FF", peerConfig.CampNameColor);

        // 位置（Other 语义）不被传播
        Assert.Equal(200, peerConfig.Left);
        Assert.Equal(300, peerConfig.Top);

        // 尺寸（RootSize 语义，默认 profile 不传播）不被传播
        Assert.Equal(200, peerConfig.Width);
        Assert.Equal(60, peerConfig.Height);

        // 数据身份字段（MapKey）不被传播
        Assert.Equal("AnotherMap", peerConfig.MapKey);

        // 文档标记为脏
        Assert.True(document.IsDirty);
    }

    // -------------------------------------------------------------------
    // 2. ApplyAppearanceToSameType_NoPeer_Disabled
    // -------------------------------------------------------------------

    /// <summary>
    /// 当文档中仅有一个某种 <see cref="FrontedControlConfigBase.ControlType"/> 的控件时，
    /// <see cref="FrontedDesignerWindowViewModel.HasSameTypePeers"/> 必须为 <see langword="false"/>，
    /// 且 <see cref="FrontedDesignerWindowViewModel.ApplyAppearanceToSameTypeCommand"/> 的 CanExecute 为 <see langword="false"/>。
    /// </summary>
    [Fact]
    public void ApplyAppearanceToSameType_NoPeer_Disabled()
    {
        var source = new FrontedControlDesignItem
        {
            Name = "Text1",
            IsSelectableInEditor = true,
            IsEditableInEditor = true,
            Config = new TextFrontedControlConfig
            {
                Color = "#FF0000",
                FontSize = 24
            }
        };
        // 不同 ControlType 的控件不构成 peer
        var otherType = new FrontedControlDesignItem
        {
            Name = "Rectangle1",
            IsSelectableInEditor = true,
            IsEditableInEditor = true,
            Config = new RectangleFrontedControlConfig
            {
                Left = 50,
                Top = 60
            }
        };
        var document = CreateDocument([source, otherType]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };
        viewModel.SelectDesignItem(source);

        Assert.False(viewModel.HasSameTypePeers);
        // Text 控件未声明 SupportsPeerStyleTransfer，按钮不可见
        Assert.False(viewModel.CanShowPeerStyleTransferButton);
        Assert.False(viewModel.ApplyAppearanceToSameTypeCommand.CanExecute(null));

        // 即使调用 Execute，也不应抛异常或修改 peer
        viewModel.ApplyAppearanceToSameTypeCommand.Execute(null);

        var otherConfig = (RectangleFrontedControlConfig)otherType.Config;
        Assert.Equal(50, otherConfig.Left);
        Assert.Equal(60, otherConfig.Top);
    }

    // -------------------------------------------------------------------
    // 3. ApplyAppearanceToSameType_DoesNotTransferPosition
    // -------------------------------------------------------------------

    /// <summary>
    /// 执行 <see cref="FrontedDesignerWindowViewModel.ApplyAppearanceToSameTypeCommand"/>
    /// 不得传播源控件的位置（<c>Left</c>/<c>Top</c>）到 peer 控件，
    /// 即使两者位置不同。
    /// </summary>
    [Fact]
    public void ApplyAppearanceToSameType_DoesNotTransferPosition()
    {
        var source = new FrontedControlDesignItem
        {
            Name = "MapV2_1",
            IsSelectableInEditor = true,
            IsEditableInEditor = true,
            Config = new MapV2DisplayControlConfig
            {
                MapKey = "ArmsFactory",
                MapNameColor = "#FF0000",
                Left = 10,
                Top = 20
            }
        };
        var peer = new FrontedControlDesignItem
        {
            Name = "MapV2_2",
            IsSelectableInEditor = true,
            IsEditableInEditor = true,
            Config = new MapV2DisplayControlConfig
            {
                MapKey = "AnotherMap",
                MapNameColor = "#00FF00",
                Left = 500,
                Top = 600
            }
        };
        var document = CreateDocument([source, peer]);
        var viewModel = new FrontedDesignerWindowViewModel(CreateMapV2Registry())
        {
            CurrentDocument = document
        };
        viewModel.SelectDesignItem(source);

        Assert.True(viewModel.HasSameTypePeers);
        Assert.True(viewModel.CanShowPeerStyleTransferButton);

        viewModel.ApplyAppearanceToSameTypeCommand.Execute(null);

        var peerConfig = (MapV2DisplayControlConfig)peer.Config;
        // 位置不被传播
        Assert.Equal(500, peerConfig.Left);
        Assert.Equal(600, peerConfig.Top);

        // Appearance 语义属性被传播
        Assert.Equal("#FF0000", peerConfig.MapNameColor);
    }

    // -------------------------------------------------------------------
    // 4. ApplyAppearanceToSameType_NoSelection_Disabled
    // -------------------------------------------------------------------

    /// <summary>
    /// 当未选中控件时，<see cref="FrontedDesignerWindowViewModel.HasSameTypePeers"/> 必须为 <see langword="false"/>，
    /// <see cref="FrontedDesignerWindowViewModel.CanShowPeerStyleTransferButton"/> 必须为 <see langword="false"/>，
    /// 且 <see cref="FrontedDesignerWindowViewModel.ApplyAppearanceToSameTypeCommand"/> 的 CanExecute 为 <see langword="false"/>。
    /// </summary>
    [Fact]
    public void ApplyAppearanceToSameType_NoSelection_Disabled()
    {
        var source = new FrontedControlDesignItem
        {
            Name = "Text1",
            IsSelectableInEditor = true,
            IsEditableInEditor = true,
            Config = new TextFrontedControlConfig()
        };
        var peer = new FrontedControlDesignItem
        {
            Name = "Text2",
            IsSelectableInEditor = true,
            IsEditableInEditor = true,
            Config = new TextFrontedControlConfig()
        };
        var document = CreateDocument([source, peer]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };

        // 未调用 SelectDesignItem
        Assert.False(viewModel.HasSameTypePeers);
        Assert.False(viewModel.CanShowPeerStyleTransferButton);
        Assert.False(viewModel.ApplyAppearanceToSameTypeCommand.CanExecute(null));
    }

    // -------------------------------------------------------------------
    // 5. ApplyAppearanceToSameType_NotShownForControlWithoutAttribute
    // -------------------------------------------------------------------

    /// <summary>
    /// 当选中控件类型未声明 <c>SupportsPeerStyleTransfer</c>（如 Text）时，
    /// 即使存在同类型 peer，<see cref="FrontedDesignerWindowViewModel.CanShowPeerStyleTransferButton"/>
    /// 也必须为 <see langword="false"/>（按钮不可见），且命令 CanExecute 为 <see langword="false"/>。
    /// </summary>
    [Fact]
    public void ApplyAppearanceToSameType_NotShownForControlWithoutAttribute()
    {
        var source = new FrontedControlDesignItem
        {
            Name = "Text1",
            IsSelectableInEditor = true,
            IsEditableInEditor = true,
            Config = new TextFrontedControlConfig { Color = "#FF0000" }
        };
        var peer = new FrontedControlDesignItem
        {
            Name = "Text2",
            IsSelectableInEditor = true,
            IsEditableInEditor = true,
            Config = new TextFrontedControlConfig { Color = "#00FF00" }
        };
        var document = CreateDocument([source, peer]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };
        viewModel.SelectDesignItem(source);

        // 存在同类型 peer
        Assert.True(viewModel.HasSameTypePeers);
        // 但 Text 未声明 SupportsPeerStyleTransfer，按钮不可见
        Assert.False(viewModel.CanShowPeerStyleTransferButton);
        Assert.False(viewModel.ApplyAppearanceToSameTypeCommand.CanExecute(null));

        // 即使调用 Execute，也不应修改 peer
        viewModel.ApplyAppearanceToSameTypeCommand.Execute(null);

        var peerConfig = (TextFrontedControlConfig)peer.Config;
        Assert.Equal("#00FF00", peerConfig.Color);
    }

    // -------------------------------------------------------------------
    // 6. ApplyAppearanceToSameType_ShownForMapV2WithAttribute
    // -------------------------------------------------------------------

    /// <summary>
    /// 当选中声明了 <c>SupportsPeerStyleTransfer</c> 的控件（如 MapV2Display）且存在同类型 peer 时，
    /// <see cref="FrontedDesignerWindowViewModel.CanShowPeerStyleTransferButton"/> 必须为 <see langword="true"/>（按钮可见），
    /// 且 <see cref="FrontedDesignerWindowViewModel.ApplyAppearanceToSameTypeCommand"/> 的 CanExecute 为 <see langword="true"/>（按钮可用）。
    /// </summary>
    [Fact]
    public void ApplyAppearanceToSameType_ShownForMapV2WithAttribute()
    {
        var source = new FrontedControlDesignItem
        {
            Name = "MapV2_1",
            IsSelectableInEditor = true,
            IsEditableInEditor = true,
            Config = new MapV2DisplayControlConfig
            {
                MapKey = "ArmsFactory",
                MapNameColor = "#FF0000"
            }
        };
        var peer = new FrontedControlDesignItem
        {
            Name = "MapV2_2",
            IsSelectableInEditor = true,
            IsEditableInEditor = true,
            Config = new MapV2DisplayControlConfig
            {
                MapKey = "AnotherMap",
                MapNameColor = "#00FF00"
            }
        };
        var document = CreateDocument([source, peer]);
        var viewModel = new FrontedDesignerWindowViewModel(CreateMapV2Registry())
        {
            CurrentDocument = document
        };
        viewModel.SelectDesignItem(source);

        Assert.True(viewModel.HasSameTypePeers);
        Assert.True(viewModel.CanShowPeerStyleTransferButton);
        Assert.True(viewModel.ApplyAppearanceToSameTypeCommand.CanExecute(null));
    }

    // -------------------------------------------------------------------
    // 7. ApplyAppearanceToSameType_MapV2NoPeer_Disabled
    // -------------------------------------------------------------------

    /// <summary>
    /// 当选中声明了 <c>SupportsPeerStyleTransfer</c> 的控件（如 MapV2Display）但不存在同类型 peer 时，
    /// <see cref="FrontedDesignerWindowViewModel.CanShowPeerStyleTransferButton"/> 必须为 <see langword="true"/>（按钮可见），
    /// 但 <see cref="FrontedDesignerWindowViewModel.ApplyAppearanceToSameTypeCommand"/> 的 CanExecute 为 <see langword="false"/>（按钮禁用）。
    /// </summary>
    [Fact]
    public void ApplyAppearanceToSameType_MapV2NoPeer_Disabled()
    {
        var source = new FrontedControlDesignItem
        {
            Name = "MapV2_1",
            IsSelectableInEditor = true,
            IsEditableInEditor = true,
            Config = new MapV2DisplayControlConfig
            {
                MapKey = "ArmsFactory",
                MapNameColor = "#FF0000"
            }
        };
        // 不同 ControlType 的控件不构成 peer
        var otherType = new FrontedControlDesignItem
        {
            Name = "Text1",
            IsSelectableInEditor = true,
            IsEditableInEditor = true,
            Config = new TextFrontedControlConfig { Color = "#00FF00" }
        };
        var document = CreateDocument([source, otherType]);
        var viewModel = new FrontedDesignerWindowViewModel(CreateMapV2Registry())
        {
            CurrentDocument = document
        };
        viewModel.SelectDesignItem(source);

        // 不存在同类型 peer
        Assert.False(viewModel.HasSameTypePeers);
        // 但 MapV2 声明了 SupportsPeerStyleTransfer，按钮仍可见
        Assert.True(viewModel.CanShowPeerStyleTransferButton);
        // 没有 peer，按钮禁用
        Assert.False(viewModel.ApplyAppearanceToSameTypeCommand.CanExecute(null));

        // 即使调用 Execute，也不应修改 otherType
        viewModel.ApplyAppearanceToSameTypeCommand.Execute(null);

        var otherConfig = (TextFrontedControlConfig)otherType.Config;
        Assert.Equal("#00FF00", otherConfig.Color);
    }

    // -------------------------------------------------------------------
    // 8. ApplyParentStyleToChildren_TransfersAppearanceToCells
    // -------------------------------------------------------------------

    /// <summary>
    /// 执行 <see cref="FrontedDesignerWindowViewModel.ApplyParentStyleToChildrenCommand"/>
    /// 必须将父控件的外观属性（<c>FontFamily</c>）传播到所有 Cell；
    /// 且 <see cref="FrontedDesignerWindowViewModel.HasChildAppearanceProperties"/> 为 <see langword="true"/>。
    /// </summary>
    [Fact]
    public void ApplyParentStyleToChildren_TransfersAppearanceToCells()
    {
        var config = CreateGlobalScoreRowConfig(fontFamily: "Consolas");
        var source = CreateDesignItem("ScoreRow_1", config);
        var document = CreateDocument([source]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };
        viewModel.SelectDesignItem(source);

        Assert.True(viewModel.HasChildAppearanceProperties);
        Assert.True(viewModel.ApplyParentStyleToChildrenCommand.CanExecute(null));

        viewModel.ApplyParentStyleToChildrenCommand.Execute(null);

        foreach (var cell in config.Cells)
        {
            Assert.Equal("Consolas", cell.FontFamily);
        }

        Assert.True(document.IsDirty);
    }

    // -------------------------------------------------------------------
    // 9. ApplyParentStyleToChildren_DoesNotTransferGeometry
    // -------------------------------------------------------------------

    /// <summary>
    /// 执行 <see cref="FrontedDesignerWindowViewModel.ApplyParentStyleToChildrenCommand"/>
    /// 不得修改 Cell 的几何属性（<c>X</c>/<c>Y</c>/<c>Width</c>/<c>Height</c>）。
    /// </summary>
    [Fact]
    public void ApplyParentStyleToChildren_DoesNotTransferGeometry()
    {
        var config = CreateGlobalScoreRowConfig(fontFamily: "Consolas");
        // 记录每个 Cell 的原始几何值。
        var originalGeometry = config.Cells
            .Select(c => (c.X, c.Y, c.Width, c.Height))
            .ToList();

        var source = CreateDesignItem("ScoreRow_1", config);
        var document = CreateDocument([source]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };
        viewModel.SelectDesignItem(source);

        viewModel.ApplyParentStyleToChildrenCommand.Execute(null);

        for (var i = 0; i < config.Cells.Count; i++)
        {
            var cell = config.Cells[i];
            var original = originalGeometry[i];
            Assert.Equal(original.X, cell.X);
            Assert.Equal(original.Y, cell.Y);
            Assert.Equal(original.Width, cell.Width);
            Assert.Equal(original.Height, cell.Height);
        }
    }

    // -------------------------------------------------------------------
    // 10. ApplyParentStyleToChildren_DoesNotTransferDataIdentity
    // -------------------------------------------------------------------

    /// <summary>
    /// 执行 <see cref="FrontedDesignerWindowViewModel.ApplyParentStyleToChildrenCommand"/>
    /// 不得修改 Cell 的数据身份字段（<c>Id</c>/<c>GameNumber</c>/<c>GameKind</c>/<c>HalfKind</c>）。
    /// </summary>
    [Fact]
    public void ApplyParentStyleToChildren_DoesNotTransferDataIdentity()
    {
        var config = CreateGlobalScoreRowConfig(fontFamily: "Consolas");
        var originalIdentity = config.Cells
            .Select(c => (c.Id, c.GameNumber, c.GameKind, c.HalfKind))
            .ToList();

        var source = CreateDesignItem("ScoreRow_1", config);
        var document = CreateDocument([source]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };
        viewModel.SelectDesignItem(source);

        viewModel.ApplyParentStyleToChildrenCommand.Execute(null);

        for (var i = 0; i < config.Cells.Count; i++)
        {
            var cell = config.Cells[i];
            var original = originalIdentity[i];
            Assert.Equal(original.Id, cell.Id);
            Assert.Equal(original.GameNumber, cell.GameNumber);
            Assert.Equal(original.GameKind, cell.GameKind);
            Assert.Equal(original.HalfKind, cell.HalfKind);
        }
    }

    // -------------------------------------------------------------------
    // 11. ClearChildStyleOverrides_ClearsNullableAppearance
    // -------------------------------------------------------------------

    /// <summary>
    /// 执行 <see cref="FrontedDesignerWindowViewModel.ClearChildStyleOverridesCommand"/>
    /// 必须清除 Cell 上的可空外观属性 override（<c>Color</c>），使其回退到父值。
    /// </summary>
    [Fact]
    public void ClearChildStyleOverrides_ClearsNullableAppearance()
    {
        var config = CreateGlobalScoreRowConfig(fontFamily: "Consolas");
        // 给第一个 Cell 设置 Color override。
        config.Cells[0].Color = "Red";
        config.Cells[0].FontFamily = "Arial";

        var source = CreateDesignItem("ScoreRow_1", config);
        var document = CreateDocument([source]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };
        viewModel.SelectDesignItem(source);

        Assert.True(viewModel.ClearChildStyleOverridesCommand.CanExecute(null));

        viewModel.ClearChildStyleOverridesCommand.Execute(null);

        // 可空外观属性应被清除为 null。
        Assert.Null(config.Cells[0].Color);
        Assert.Null(config.Cells[0].FontFamily);

        Assert.True(document.IsDirty);
    }

    // -------------------------------------------------------------------
    // 12. HasChildAppearanceProperties_FalseForBorderedImage
    // -------------------------------------------------------------------

    /// <summary>
    /// 当选中 <c>BorderedImage</c> 控件（其 Image Part 无外观属性工厂）时，
    /// <see cref="FrontedDesignerWindowViewModel.HasChildAppearanceProperties"/> 必须为 <see langword="false"/>。
    /// </summary>
    [Fact]
    public void HasChildAppearanceProperties_FalseForBorderedImage()
    {
        var source = CreateDesignItem(
            "Image_1",
            new BorderedImageFrontedControlConfig
            {
                ControlType = "BorderedImage",
                ImagePath = "test.png"
            });
        var document = CreateDocument([source]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };
        viewModel.SelectDesignItem(source);

        Assert.False(viewModel.HasChildAppearanceProperties);
        Assert.False(viewModel.ApplyParentStyleToChildrenCommand.CanExecute(null));
        Assert.False(viewModel.ClearChildStyleOverridesCommand.CanExecute(null));
    }

    // -------------------------------------------------------------------
    // 13. HasChildAppearanceProperties_TrueForGlobalScoreRow
    // -------------------------------------------------------------------

    /// <summary>
    /// 当选中 <c>GlobalScoreRow</c> 控件（其 Cells 集合定义了外观属性工厂）时，
    /// <see cref="FrontedDesignerWindowViewModel.HasChildAppearanceProperties"/> 必须为 <see langword="true"/>。
    /// </summary>
    [Fact]
    public void HasChildAppearanceProperties_TrueForGlobalScoreRow()
    {
        var config = CreateGlobalScoreRowConfig();
        var source = CreateDesignItem("ScoreRow_1", config);
        var document = CreateDocument([source]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };
        viewModel.SelectDesignItem(source);

        Assert.True(viewModel.HasChildAppearanceProperties);
        Assert.True(viewModel.ApplyParentStyleToChildrenCommand.CanExecute(null));
        Assert.True(viewModel.ClearChildStyleOverridesCommand.CanExecute(null));
    }

    // -------------------------------------------------------------------
    // 辅助方法
    // -------------------------------------------------------------------

    /// <summary>
    /// 创建一个 <see cref="FrontedControlDesignItem"/>，配置为指定 Config，
    /// 默认 <see cref="FrontedControlDesignItem.IsSelectableInEditor"/>/<see cref="FrontedControlDesignItem.IsEditableInEditor"/> 为 <see langword="true"/>。
    /// </summary>
    /// <param name="name">设计项名称。</param>
    /// <param name="config">控件配置。</param>
    /// <returns>用于测试的设计项。</returns>
    private static FrontedControlDesignItem CreateDesignItem(string name, FrontedControlConfigBase config)
    {
        return new FrontedControlDesignItem
        {
            Name = name,
            IsSelectableInEditor = true,
            IsEditableInEditor = true,
            Config = config
        };
    }

    /// <summary>
    /// 创建一个包含两个 Cell 的 <see cref="GlobalScoreRowControlConfig"/>，
    /// 用于父到子外观派发测试。
    /// </summary>
    /// <param name="fontFamily">父行 FontFamily；默认 <see langword="null"/>。</param>
    /// <returns>包含两个 Cell 的 GlobalScoreRow 配置。</returns>
    private static GlobalScoreRowControlConfig CreateGlobalScoreRowConfig(string? fontFamily = null)
    {
        var config = new GlobalScoreRowControlConfig
        {
            ControlType = "GlobalScoreRow",
            FontFamily = fontFamily,
            Color = "#000000",
            FontSize = 24
        };

        config.Cells.Add(new GlobalScoreCellConfig
        {
            Id = "Game1FirstHalf",
            GameNumber = 1,
            GameKind = ScoreGameKind.Normal,
            HalfKind = ScoreHalfKind.FirstHalf,
            X = 10,
            Y = 20,
            Width = 75,
            Height = 32
        });
        config.Cells.Add(new GlobalScoreCellConfig
        {
            Id = "Game1SecondHalf",
            GameNumber = 1,
            GameKind = ScoreGameKind.Normal,
            HalfKind = ScoreHalfKind.SecondHalf,
            X = 100,
            Y = 20,
            Width = 75,
            Height = 32
        });

        return config;
    }

    /// <summary>
    /// 创建一个包含指定控件列表的 <see cref="FrontedCanvasDesignDocument"/>。
    /// </summary>
    /// <param name="controls">控件设计项列表。</param>
    /// <returns>用于测试的设计文档。</returns>
    private static FrontedCanvasDesignDocument CreateDocument(
        IList<FrontedControlDesignItem> controls,
        string windowTypeName = "TestWindow")
    {
        return new FrontedCanvasDesignDocument
        {
            WindowTypeName = windowTypeName,
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

    /// <summary>
    /// 构造仅包含 <c>MapV2Display</c> 注册的 v3 控件注册表，
    /// 该注册声明 <c>SupportsPeerStyleTransfer = true</c>，
    /// 用于在测试中验证 peer 样式传播入口的门控逻辑。
    /// </summary>
    /// <returns>包含 MapV2Display 注册的 <see cref="IFrontedV3ControlRegistry"/>。</returns>
    private static IFrontedV3ControlRegistry CreateMapV2Registry()
    {
        var sampleConfig = new MapV2DisplayControlConfig { ControlType = "MapV2Display" };
        var properties = BuiltInPropertyDefinitionResolver.GetProperties(sampleConfig);

        var registration = new FrontedV3ControlRegistration
        {
            CanonicalControlType = "MapV2Display",
            LocalControlId = "MapV2Display",
            PackageId = null,
            IsBuiltIn = true,
            SupportsPeerStyleTransfer = true,
            ControlType = typeof(Border),
            ConfigType = typeof(MapV2DisplayControlConfig),
            Properties = properties,
            CreateDefaultConfig = () => new MapV2DisplayControlConfig { ControlType = "MapV2Display" }
        };

        return new FrontedV3ControlRegistry([registration]);
    }
}
