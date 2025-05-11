using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Web;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Services;

namespace neo_bpsys_wpf.Models;

/// <summary>
/// 表示系统中的成员模型类，实现可观察属性以支持MVVM模式的数据绑定
/// </summary>
public partial class Member : ObservableObject
{
    /// <summary>
    /// 默认构造函数（设计时可用）
    /// 用于XAML设计器实例化，不执行实际初始化逻辑
    /// </summary>
    public Member()
    {
        // Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private Camp _camp;

    [ObservableProperty]
    private ImageSource? _image;

    [ObservableProperty]
    private bool _isOnField = false;

    [ObservableProperty]
    private bool _canOnFieldChange = true;

    /// <summary>
    /// 使用指定阵营初始化成员实例
    /// </summary>
    /// <param name="camp">成员所属的阵营标识</param>
    public Member(Camp camp)
    {
        Camp = camp;
    }
}