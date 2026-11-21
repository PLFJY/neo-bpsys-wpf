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
/// Fronted Designer 的缩放预设、自适应与手动缩放业务逻辑。
/// </summary>
public partial class FrontedDesignerWindowViewModel
{
    private const double MinZoomPercent = 25D;
    private const double MaxZoomPercent = 400D;
    private const double MinManualZoomScale = 0.25D;
    private const double MaxManualZoomScale = 4D;

    partial void OnSelectedZoomPresetChanged(FrontedDesignerZoomPreset? value)
    {
        if (_isChangingZoomPreset || value is null)
        {
            return;
        }

        ApplyZoomPreset(value);
    }
    [RelayCommand]
    private void ZoomIn()
    {
        ApplyManualZoom(GetNextManualZoom(ZoomScale));
    }

    [RelayCommand]
    private void ZoomOut()
    {
        ApplyManualZoom(GetPreviousManualZoom(ZoomScale));
    }

    [RelayCommand]
    private void FitToWindow()
    {
        var fitPreset = ZoomPresets.FirstOrDefault(preset => preset.IsFit);
        if (fitPreset is not null)
        {
            ApplyZoomPreset(fitPreset);
        }
    }

    [RelayCommand]
    private void SetZoomPreset(object? parameter)
    {
        var displayName = Convert.ToString(parameter, CultureInfo.InvariantCulture);
        var preset = ZoomPresets.FirstOrDefault(item =>
            string.Equals(item.DisplayName, displayName, StringComparison.OrdinalIgnoreCase));
        if (preset is not null)
        {
            ApplyZoomPreset(preset);
        }
    }
    private void ApplyZoomPreset(FrontedDesignerZoomPreset preset)
    {
        if (preset.IsFit)
        {
            IsFitMode = true;
            UpdateFitZoomFromCurrentDocument();
        }
        else
        {
            ApplyManualZoom(preset.Scale);
        }

        SetSelectedZoomPreset(preset);
    }

    /// <summary>
    /// 按手动缩放的允许范围应用缩放比例。
    /// </summary>
    /// <param name="scale">目标缩放比例，1 表示 100%。</param>
    public void ApplyManualZoom(double scale)
    {
        if (!double.IsFinite(scale))
        {
            return;
        }

        ApplyManualZoomValue(Math.Clamp(scale, MinManualZoomScale, MaxManualZoomScale));
    }

    /// <summary>
    /// 应用任意正缩放值；用于下拉框手动输入，允许超出滑杆的 25%–400% 显示范围。
    /// </summary>
    /// <param name="scale">目标缩放比例，1 表示 100%。</param>
    /// <param name="updateSelection">是否同步下拉框的选中预设；拖动期间可延后到释放时更新。</param>
    private void ApplyManualZoomValue(double scale, bool updateSelection = true)
    {
        if (!double.IsFinite(scale) || scale <= 0D)
        {
            return;
        }

        var percent = scale * 100D;

        // 必须先置抑制标记，再修改 ZoomScale。ZoomScale 的属性通知是同步的，
        // 绑定会在通知过程中立刻把滑杆强制到 25%–400%，若此时标记尚未设置，
        // 端点回写会被当成用户输入并覆盖刚输入的缩放值。
        _suppressZoomSliderEndpointWriteback = percent < MinZoomPercent || percent > MaxZoomPercent;
        if (_suppressZoomSliderEndpointWriteback)
        {
            // 等待当前属性通知和滑杆强制值域完成后再解除抑制，
            // 避免把用户下一次真实点击端点误判为强制回写。
            var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() => _suppressZoomSliderEndpointWriteback = false));
        }

        IsFitMode = false;
        ZoomScale = scale;
        ZoomDisplay = FormatZoomPercent(scale);

        if (!updateSelection)
        {
            return;
        }

        var matchingPreset = ZoomPresets.FirstOrDefault(
            preset => !preset.IsFit && Math.Abs(preset.Scale - scale) < 0.001D);
        SetSelectedZoomPreset(matchingPreset);
    }

    private static string FormatZoomPercent(double scale)
    {
        var percent = scale * 100D;
        if (!double.IsFinite(percent) || percent == 0D)
        {
            return "0%";
        }

        // 整数百分比必须完整显示（如 1234%），3 位有效数字只用于带小数的值（如 45.324% -> 45.3%）。
        var roundedInteger = Math.Round(percent, MidpointRounding.AwayFromZero);
        if (Math.Abs(percent - roundedInteger) < 0.0000001D)
        {
            return $"{roundedInteger.ToString("0", CultureInfo.CurrentCulture)}%";
        }

        var absolute = Math.Abs(percent);
        if (absolute >= 1_000_000D)
        {
            return $"{percent.ToString("G3", CultureInfo.CurrentCulture)}%";
        }

        var exponent = (int)Math.Floor(Math.Log10(absolute));
        var decimals = 2 - exponent;
        string text;
        if (decimals >= 0)
        {
            var rounded = Math.Round(percent, Math.Min(decimals, 15), MidpointRounding.AwayFromZero);
            text = rounded.ToString(
                "0." + new string('#', Math.Min(decimals, 15)),
                CultureInfo.CurrentCulture);
        }
        else
        {
            var factor = Math.Pow(10D, -decimals);
            var rounded = Math.Round(percent / factor, MidpointRounding.AwayFromZero) * factor;
            text = rounded.ToString("0", CultureInfo.CurrentCulture);
        }

        return $"{text}%";
    }

    public void ZoomByWheelDelta(int delta)
    {
        if (delta == 0)
        {
            return;
        }

        var multiplier = delta > 0 ? 1.1D : 1D / 1.1D;
        ApplyManualZoom(ZoomScale * multiplier);
    }

    /// <summary>
    /// 更新设计表面实际内容尺寸（含拖到画布外的控件），供适应模式计算缩放和居中范围。
    /// </summary>
    /// <param name="width">设计表面内容宽度。</param>
    /// <param name="height">设计表面内容高度。</param>
    public void UpdateDesignSurfaceContentSize(double width, double height)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0D || height <= 0D)
        {
            return;
        }

        _designSurfaceContentWidth = width;
        _designSurfaceContentHeight = height;
    }

    /// <summary>
    /// 更新适应模式使用的实际内容尺寸；不包含设计表面为拖拽预留的空白边距。
    /// </summary>
    /// <param name="width">实际内容宽度。</param>
    /// <param name="height">实际内容高度。</param>
    public void UpdateDesignSurfaceFitSize(double width, double height)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0D || height <= 0D)
        {
            return;
        }

        _designSurfaceFitWidth = width;
        _designSurfaceFitHeight = height;
    }

    public void UpdateFitZoom(double viewportWidth, double viewportHeight)
    {
        _lastPreviewViewportWidth = viewportWidth;
        _lastPreviewViewportHeight = viewportHeight;
        UpdateFitZoomFromCurrentDocument();
    }

    public void UpdateFitZoom(
        double viewportWidth,
        double viewportHeight,
        double canvasWidth,
        double canvasHeight)
    {
        _lastPreviewViewportWidth = viewportWidth;
        _lastPreviewViewportHeight = viewportHeight;

        if (!IsFitMode)
        {
            return;
        }

        if (_designSurfaceFitWidth > 0D && _designSurfaceFitHeight > 0D)
        {
            UpdateFitZoomFromCurrentDocument();
            return;
        }

        ApplyFitZoom(viewportWidth, viewportHeight, canvasWidth, canvasHeight);
    }

    public static double CalculateFitZoom(
        double viewportWidth,
        double viewportHeight,
        double canvasWidth,
        double canvasHeight,
        double padding = 0D)
    {
        if (canvasWidth <= 0D || canvasHeight <= 0D)
        {
            return 1D;
        }

        var availableWidth = Math.Max(1D, viewportWidth - padding);
        var availableHeight = Math.Max(1D, viewportHeight - padding);
        var scale = Math.Min(availableWidth / canvasWidth, availableHeight / canvasHeight);
        return Math.Clamp(scale, 0.05D, 4D);
    }

    private double GetNextManualZoom(double currentScale)
    {
        return ZoomPresets
            .Where(preset => !preset.IsFit && preset.Scale > currentScale + 0.001D)
            .OrderBy(preset => preset.Scale)
            .FirstOrDefault()?.Scale ?? 4D;
    }

    private double GetPreviousManualZoom(double currentScale)
    {
        return ZoomPresets
            .Where(preset => !preset.IsFit && preset.Scale < currentScale - 0.001D)
            .OrderByDescending(preset => preset.Scale)
            .FirstOrDefault()?.Scale ?? 0.25D;
    }

    private void InitializeZoomPresets()
    {
        if (ZoomPresets.Count > 0)
        {
            return;
        }

        ZoomPresets.Add(new FrontedDesignerZoomPreset("Fit", 0D, isFit: true));
        ZoomPresets.Add(new FrontedDesignerZoomPreset("25%", 0.25D));
        ZoomPresets.Add(new FrontedDesignerZoomPreset("50%", 0.5D));
        ZoomPresets.Add(new FrontedDesignerZoomPreset("75%", 0.75D));
        ZoomPresets.Add(new FrontedDesignerZoomPreset("100%", 1D));
        ZoomPresets.Add(new FrontedDesignerZoomPreset("125%", 1.25D));
        ZoomPresets.Add(new FrontedDesignerZoomPreset("150%", 1.5D));
        ZoomPresets.Add(new FrontedDesignerZoomPreset("200%", 2D));
        ZoomPresets.Add(new FrontedDesignerZoomPreset("300%", 3D));
        ZoomPresets.Add(new FrontedDesignerZoomPreset("400%", 4D));
    }

    private void UpdateFitZoomFromCurrentDocument()
    {
        if (!IsFitMode)
        {
            return;
        }

        var canvas = CurrentDocument?.CanvasConfig;
        if (canvas is null)
        {
            ZoomScale = 1D;
            ZoomDisplay = FormatZoomPercent(ZoomScale);
            return;
        }

        // 画布外的控件会扩展设计表面；适应模式必须按实际内容范围计算，否则会裁掉溢出的控件。
        // 设计表面为拖拽预留的空白边距不参与适应，否则点击“适应”后会在四周留下明显空白。
        var contentWidth = _designSurfaceFitWidth > 0D ? _designSurfaceFitWidth : canvas.CanvasWidth;
        var contentHeight = _designSurfaceFitHeight > 0D ? _designSurfaceFitHeight : canvas.CanvasHeight;
        ApplyFitZoom(
            _lastPreviewViewportWidth,
            _lastPreviewViewportHeight,
            contentWidth,
            contentHeight);
    }

    private void ApplyFitZoom(
        double viewportWidth,
        double viewportHeight,
        double canvasWidth,
        double canvasHeight)
    {
        var previousScale = ZoomScale;
        // ZoomHost 外框 BorderThickness=1 会在缩放后额外占 2px；
        // 预留这 2px，避免适应后因 extent 超出视口而出现滚动条。
        var fitScale = CalculateFitZoom(viewportWidth, viewportHeight, canvasWidth, canvasHeight, padding: 2D);
        var fitPercent = fitScale * 100D;
        _suppressZoomSliderEndpointWriteback = fitPercent < MinZoomPercent || fitPercent > MaxZoomPercent;
        if (_suppressZoomSliderEndpointWriteback)
        {
            // 内容过大/过小时适应比例会超出滑杆 25%–400% 的显示范围；
            // 滑杆会被 RangeBase 强制到端点，必须忽略这次端点回写，否则会覆盖适应比例。
            var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() => _suppressZoomSliderEndpointWriteback = false));
        }

        ZoomScale = fitScale;
        ZoomDisplay = FormatZoomPercent(ZoomScale);

        // 点击“适应”时即使缩放比例未变化，也要重新居中；ZoomScale 未变化不会触发属性变更，
        // 这里主动通知一次，让视图重置平移和滚动位置。
        if (Math.Abs(previousScale - ZoomScale) < 0.000001D)
        {
            OnPropertyChanged(nameof(ZoomScale));
        }
    }

    private void SetSelectedZoomPreset(FrontedDesignerZoomPreset? preset)
    {
        _isChangingZoomPreset = true;
        SelectedZoomPreset = preset;
        _isChangingZoomPreset = false;
    }

    /// <summary>
    /// 应用下拉框中输入的缩放文本。
    /// </summary>
    /// <param name="text">支持百分比（如 100）、带百分号（100%）或倍数（1x）的文本；数值必须是正的 C# float。</param>
    /// <returns>成功应用时返回 <see langword="true"/>。</returns>
    public bool TryApplyZoomText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            UpdateZoomEditTextFromCurrentZoom();
            return false;
        }

        text = text.Trim();

        var fitKey = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Fit");
        if (string.Equals(text, fitKey, StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "Fit", StringComparison.OrdinalIgnoreCase))
        {
            FitToWindow();
            return true;
        }

        double scale;
        if (text.EndsWith("x", StringComparison.OrdinalIgnoreCase))
        {
            var numericPart = text.AsSpan(0, text.Length - 1).Trim();
            if (!TryParsePositiveFloat(numericPart, out var multiplier))
            {
                SetInvalidZoom();
                return false;
            }

            scale = multiplier;
        }
        else if (text.EndsWith("%", StringComparison.Ordinal))
        {
            var numericPart = text.AsSpan(0, text.Length - 1).Trim();
            if (!TryParsePositiveFloat(numericPart, out var percent))
            {
                SetInvalidZoom();
                return false;
            }

            scale = percent / 100D;
        }
        else
        {
            if (!TryParsePositiveFloat(text, out var plainNumber))
            {
                SetInvalidZoom();
                return false;
            }

            scale = plainNumber / 100D;
        }

        ApplyManualZoomValue(scale);
        return true;
    }

    private static bool TryParsePositiveFloat(ReadOnlySpan<char> text, out double value)
    {
        value = 0D;
        if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            || !float.IsFinite(parsed)
            || parsed <= 0F)
        {
            return false;
        }

        value = parsed;
        return true;
    }

    private void SetInvalidZoom()
    {
        StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.ZoomInvalid");
        UpdateZoomEditTextFromCurrentZoom();
    }

    public void ApplyZoomPercent(double percent)
    {
        ZoomPercent = percent;
    }

    private void UpdateZoomEditTextFromCurrentZoom()
    {
        if (IsFitMode)
        {
            ZoomEditText = FormatZoomPercent(ZoomScale);
        }
        else
        {
            ZoomEditText = FormatZoomPercent(ZoomScale);
        }
    }
}
