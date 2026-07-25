#nullable enable
using System.Collections.Generic;
using System.Linq;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3;
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
/// <item>命令仅在同窗口存在相同 <see cref="FrontedControlConfigBase.ControlType"/> 的其他控件时启用。</item>
/// <item>执行命令时，<see cref="FrontedV3PropertySemantic.Appearance"/> 语义的属性被传播到 peer。</item>
/// <item>位置（<c>Left</c>/<c>Top</c>）与尺寸（<c>Width</c>/<c>Height</c>）等非 Appearance 语义属性不被传播。</item>
/// <item>数据身份字段（<c>BindingPath</c>）不被传播。</item>
/// </list>
/// </para>
/// <para>
/// 这些测试通过设计时构造函数构造 ViewModel，使用 <see cref="TextFrontedControlConfig"/>
/// 作为内置控件示例。该 Config 的 <c>Color</c>/<c>FontSize</c> 属性为 Appearance 语义，
/// <c>Left</c>/<c>Top</c> 为布局（Other 语义），<c>Width</c>/<c>Height</c> 为 RootSize 语义，
/// <c>BindingPath</c> 为 DataIdentity 语义。
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
    /// 必须将源控件的 Appearance 语义属性（<c>Color</c>、<c>FontSize</c>）传播到 peer 控件；
    /// 而 <c>Left</c>、<c>Top</c>、<c>Width</c>、<c>Height</c>、<c>BindingPath</c> 不被传播。
    /// </summary>
    [Fact]
    public void ApplyAppearanceToSameType_TransfersOnlyAppearance()
    {
        var source = new FrontedControlDesignItem
        {
            Name = "Text1",
            IsSelectableInEditor = true,
            IsEditableInEditor = true,
            Config = new TextFrontedControlConfig
            {
                Text = "Source",
                Color = "#FF0000",
                FontSize = 24,
                Left = 10,
                Top = 20,
                Width = 100,
                Height = 30,
                BindingPath = "ScoreA"
            }
        };
        var peer = new FrontedControlDesignItem
        {
            Name = "Text2",
            IsSelectableInEditor = true,
            IsEditableInEditor = true,
            Config = new TextFrontedControlConfig
            {
                Text = "Peer",
                Color = "#00FF00",
                FontSize = 12,
                Left = 200,
                Top = 300,
                Width = 200,
                Height = 60,
                BindingPath = "ScoreB"
            }
        };
        var document = CreateDocument([source, peer]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };
        viewModel.SelectDesignItem(source);

        Assert.True(viewModel.HasSameTypePeers);
        Assert.True(viewModel.ApplyAppearanceToSameTypeCommand.CanExecute(null));

        viewModel.ApplyAppearanceToSameTypeCommand.Execute(null);

        var peerConfig = (TextFrontedControlConfig)peer.Config;
        // Appearance 语义属性被传播
        Assert.Equal("#FF0000", peerConfig.Color);
        Assert.Equal(24, peerConfig.FontSize);

        // 位置（Other 语义）不被传播
        Assert.Equal(200, peerConfig.Left);
        Assert.Equal(300, peerConfig.Top);

        // 尺寸（RootSize 语义，默认 profile 不传播）不被传播
        Assert.Equal(200, peerConfig.Width);
        Assert.Equal(60, peerConfig.Height);

        // 数据身份字段不被传播
        Assert.Equal("ScoreB", peerConfig.BindingPath);

        // 文本内容（Other 语义）不被传播
        Assert.Equal("Peer", peerConfig.Text);

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
            Name = "Text1",
            IsSelectableInEditor = true,
            IsEditableInEditor = true,
            Config = new TextFrontedControlConfig
            {
                Color = "#FF0000",
                Left = 10,
                Top = 20
            }
        };
        var peer = new FrontedControlDesignItem
        {
            Name = "Text2",
            IsSelectableInEditor = true,
            IsEditableInEditor = true,
            Config = new TextFrontedControlConfig
            {
                Color = "#00FF00",
                Left = 500,
                Top = 600
            }
        };
        var document = CreateDocument([source, peer]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };
        viewModel.SelectDesignItem(source);

        Assert.True(viewModel.HasSameTypePeers);

        viewModel.ApplyAppearanceToSameTypeCommand.Execute(null);

        var peerConfig = (TextFrontedControlConfig)peer.Config;
        // 位置不被传播
        Assert.Equal(500, peerConfig.Left);
        Assert.Equal(600, peerConfig.Top);

        // Appearance 语义属性被传播
        Assert.Equal("#FF0000", peerConfig.Color);
    }

    // -------------------------------------------------------------------
    // 4. ApplyAppearanceToSameType_NoSelection_Disabled
    // -------------------------------------------------------------------

    /// <summary>
    /// 当未选中控件时，<see cref="FrontedDesignerWindowViewModel.HasSameTypePeers"/> 必须为 <see langword="false"/>，
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
        Assert.False(viewModel.ApplyAppearanceToSameTypeCommand.CanExecute(null));
    }

    // -------------------------------------------------------------------
    // 辅助方法
    // -------------------------------------------------------------------

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
}
