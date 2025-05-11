using CommunityToolkit.Mvvm.ComponentModel;

namespace neo_bpsys_wpf.Models;

/// <summary>
/// 表示一个得分记录实体类，用于存储对战成绩统计信息
/// 继承自ObservableObject以支持MVVM模式下的属性变更通知
/// </summary>
public partial class Score : ObservableObject
{
    /// <summary>
    /// 获取或设置胜利次数属性
    /// 自动触发INotifyPropertyChanged接口的变更通知
    /// </summary>
    [ObservableProperty]
    private int _win = 0;

    /// <summary>
    /// 获取或设置失败次数属性
    /// 自动触发INotifyPropertyChanged接口的变更通知
    /// </summary>
    [ObservableProperty]
    private int _lose = 0;

    /// <summary>
    /// 获取或设置平局次数属性
    /// 自动触发INotifyPropertyChanged接口的变更通知
    /// </summary>
    [ObservableProperty]
    private int _tie = 0;

    /// <summary>
    /// 获取或设置附加积分属性（如小分、奖励分等）
    /// 自动触发INotifyPropertyChanged接口的变更通知
    /// </summary>
    [ObservableProperty]
    private int _minorPoints = 0;
}