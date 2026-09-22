using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.ExamplePlugin.ViewModels;

public partial class MainPageViewModel : ViewModelBase
{
    private readonly IFrontedBehaviorEventPublisher<ExamplePlugin> _behaviorEventPublisher;

    /// <summary>
    /// 初始化示例插件后台页面视图模型。
    /// </summary>
    /// <param name="behaviorEventPublisher">绑定当前示例插件身份的行为事件发布器。</param>
    public MainPageViewModel(IFrontedBehaviorEventPublisher<ExamplePlugin> behaviorEventPublisher)
    {
        _behaviorEventPublisher = behaviorEventPublisher;
    }

    /// <summary>
    /// 获取或设置示例编辑文本。
    /// </summary>
    [ObservableProperty]
    public partial string EditedText { get; set; } = "ExamplePlugin";

    [RelayCommand]
    private void Confirm()
    {
        // The v3 example no longer mutates an injected frontend control.
    }

    /// <summary>
    /// 获取当前示例计数器值。
    /// </summary>
    [ObservableProperty]
    public partial int Counter { get; set; }

    /// <summary>
    /// 获取最近发布的示例事件说明。
    /// </summary>
    [ObservableProperty]
    public partial string LastPublishedEvent { get; set; } = "No behavior event has been published yet.";

    [RelayCommand]
    private void Plus1()
    {
        Counter++;
        _behaviorEventPublisher.Publish(
            "CounterChanged",
            new Dictionary<string, object?>
            {
                ["CounterValue"] = Counter,
                ["Delta"] = 1
            });
        LastPublishedEvent = $"Published CounterChanged: CounterValue={Counter}, Delta=1";
    }

    [RelayCommand]
    private void StartPulse()
    {
        _behaviorEventPublisher.Publish("StartPulse");
        LastPublishedEvent = "Published StartPulse.";
    }

    [RelayCommand]
    private void StopPulse()
    {
        _behaviorEventPublisher.Publish("StopPulse");
        LastPublishedEvent = "Published StopPulse.";
    }
}
