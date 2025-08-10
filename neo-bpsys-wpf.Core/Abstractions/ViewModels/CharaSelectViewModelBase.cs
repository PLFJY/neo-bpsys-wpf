using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Messages;
using neo_bpsys_wpf.Core.Models;

namespace neo_bpsys_wpf.Core.Abstractions.ViewModels;

/// <summary>
/// 用于选择角色的角色选择器行为的基类
/// 需要派生类所做的是: <br/>
/// 1.设置<see cref="CharaList"/><br/>
/// 2.设置<see cref="IsEnabled"/><br/>
/// 3.实现<see cref="SyncCharaAsync"/>
/// </summary>
public abstract partial class CharaSelectViewModelBase :
    ViewModelBase,
    IRecipient<HighlightMessage>
{
    protected readonly ISharedDataService SharedDataService;

    /// <summary>
    /// 当前的序号
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// 当前选中的角色
    /// </summary>
    [ObservableProperty] private Character? _selectedChara;

    /// <summary>
    /// 预览图片
    /// </summary>
    [ObservableProperty] private ImageSource? _previewImage;
    
    private bool _isEnabled = true;
    
    /// <summary>
    /// 当前选择器是否可用
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set =>
            SetPropertyWithAction(ref _isEnabled, value, _ =>
            {
                SyncIsEnabled();
                OnPropertyChanged();
            });
    }
    /// <summary>
    /// 当前选择器是否高亮
    /// </summary>
    [ObservableProperty] private bool _isHighlighted;
    
    /// <summary>
    /// 对应的互换器是否高亮
    /// </summary>
    [ObservableProperty] private bool _isCharaChangerHighlighted;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="sharedDataService">共享数据服务</param>
    /// <param name="index">当前序号</param>
    protected CharaSelectViewModelBase(ISharedDataService sharedDataService, int index = 0)
    {
        SharedDataService = sharedDataService;
        Index = index;

        SharedDataService.CurrentGameChanged += OnCurrentGameChanged;
    }
    
    /// <summary>
    /// 角色列表
    /// </summary>
    public Dictionary<string, Character> CharaList { get; set; } = [];
    
    /// <summary>
    /// 确认
    /// </summary>
    /// <returns></returns>
    public abstract Task SyncCharaAsync();
    /// <summary>
    /// 同步当前角色选择器是否启用状态
    /// </summary>
    protected abstract void SyncIsEnabled();
    /// <summary>
    /// 判断当前高亮步骤是否符合当前控件
    /// </summary>
    /// <param name="action">当前步骤</param>
    /// <returns>是否符合</returns>
    protected abstract bool IsActionNameCorrect(GameAction? action);
    
    /// <summary>
    /// 确认命令
    /// </summary>
    [RelayCommand]
    private async Task ConfirmAsync() => await SyncCharaAsync();
    
    /// <summary>
    /// 新对局事件
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    private void OnCurrentGameChanged(object? sender, EventArgs args)
    {
        SelectedChara = null;
        PreviewImage = null;
    }
    
    /// <summary>
    /// 接收高亮消息
    /// </summary>
    /// <param name="message">消息</param>
    public void Receive(HighlightMessage message)
    {
        if (IsActionNameCorrect(message.GameAction) && message.Index != null && message.Index.Contains(Index))
        {
            IsHighlighted = true;
        }
        else
        {
            IsHighlighted = false;
        }

        IsCharaChangerHighlighted = message.GameAction == GameAction.DistributeChara;
    }
}