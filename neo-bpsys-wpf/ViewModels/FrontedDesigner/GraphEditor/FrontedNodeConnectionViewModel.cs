using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Tutorial;

namespace neo_bpsys_wpf.ViewModels.FrontedDesigner.GraphEditor;

public sealed partial class FrontedNodeConnectionViewModel(
    FrontedNodeConnection model,
    FrontedNodeEditorViewModel source,
    FrontedNodeEditorViewModel target,
    Func<string, string, string>? localize = null) : ObservableObject
{
    private readonly Func<string, string, string>? _localize = localize;

    public FrontedNodeConnection Model { get; } = model;
    public FrontedNodeEditorViewModel Source { get; } = source;
    public FrontedNodeEditorViewModel Target { get; } = target;
    public string Summary => $"{Source.DisplayName}.{Model.SourcePort} -> {Target.DisplayName}.{Model.TargetPort}";
    /// <summary>获取该连接的源端口视图模型。</summary>
    public FrontedNodePortViewModel? SourcePort => Source.OutputPorts.FirstOrDefault(port => port.Name == Model.SourcePort);
    /// <summary>获取该连接的目标端口视图模型。</summary>
    public FrontedNodePortViewModel? TargetPort => Target.InputPorts.FirstOrDefault(port => port.Name == Model.TargetPort);
    /// <summary>获取可见源端口名称。</summary>
    public string SourcePortDisplayName => SourcePort?.DisplayName ?? Model.SourcePort;
    /// <summary>获取可见目标端口名称。</summary>
    public string TargetPortDisplayName => TargetPort?.DisplayName ?? Model.TargetPort;
    /// <summary>获取连接源侧的语义含义。</summary>
    public string Meaning => SourcePort?.Meaning ?? SourcePortDisplayName;
    /// <summary>获取根据源端口角色派生的连接描边颜色。</summary>
    public string StrokeColorHex => SourcePort?.PortColorHex ?? "#1976D2";
    /// <summary>获取根据源端口角色派生的连接描边粗细。</summary>
    public double StrokeThickness => SourcePort?.IsParallelContinuation == true ? 4D : 3D;
    /// <summary>获取悬停连接时显示的连接检查文本。</summary>
    public string InspectionText => $"{Source.DisplayName}.{Model.SourcePort} -> {Target.DisplayName}.{Model.TargetPort}{Environment.NewLine}"
                                    + $"{Source.DisplayName}: {SourcePortDisplayName}{Environment.NewLine}"
                                    + $"{Target.DisplayName}: {TargetPortDisplayName}{Environment.NewLine}"
                                    + $"{Localize("Designer.Graph.Connection.MeaningLabel", "Meaning")}: {Meaning}";
    public double X1 => Source.X + Source.CardWidth;
    public double Y1 => Source.Y + (SourcePort?.CenterOffsetY ?? 56D);
    public double X2 => Target.X;
    public double Y2 => Target.Y + (TargetPort?.CenterOffsetY ?? 56D);

    /// <summary>贝塞尔曲线控制点 1 X（从起点水平向右延伸）</summary>
    public double CP1X => X1 + CurveOffset;
    /// <summary>贝塞尔曲线控制点 1 Y</summary>
    public double CP1Y => Y1;
    /// <summary>贝塞尔曲线控制点 2 X（从终点水平向左延伸）</summary>
    public double CP2X => X2 - CurveOffset;
    /// <summary>贝塞尔曲线控制点 2 Y</summary>
    public double CP2Y => Y2;

    private double CurveOffset => Math.Max(60, Math.Abs(X2 - X1) * 0.45);

    /// <summary>贝塞尔曲线 Path 数据（StreamGeometry 小语言格式）</summary>
    public string PathData => $"M {X1:F1},{Y1:F1} C {CP1X:F1},{CP1Y:F1} {CP2X:F1},{CP2Y:F1} {X2:F1},{Y2:F1}";

    public double MidX => (X1 + X2) / 2D - 12D;
    public double MidY => (Y1 + Y2) / 2D - 12D;

    public void Refresh()
    {
        OnPropertyChanged(nameof(X1));
        OnPropertyChanged(nameof(Y1));
        OnPropertyChanged(nameof(X2));
        OnPropertyChanged(nameof(Y2));
        OnPropertyChanged(nameof(CP1X));
        OnPropertyChanged(nameof(CP1Y));
        OnPropertyChanged(nameof(CP2X));
        OnPropertyChanged(nameof(CP2Y));
        OnPropertyChanged(nameof(PathData));
        OnPropertyChanged(nameof(MidX));
        OnPropertyChanged(nameof(MidY));
        OnPropertyChanged(nameof(InspectionText));
    }

    private string Localize(string key, string fallback) =>
        _localize?.Invoke(key, fallback) ?? fallback;
}
