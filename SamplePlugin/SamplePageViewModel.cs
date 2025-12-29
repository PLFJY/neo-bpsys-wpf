using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SamplePlugin;

/// <summary>
/// 示例插件页面的ViewModel
/// </summary>
public partial class SamplePageViewModel : ObservableObject
{
    [ObservableProperty]
    private string _message = "这是示例插件的页面";

    [ObservableProperty]
    private int _counter = 0;

    /// <summary>
    /// 设置变更回调
    /// </summary>
    public Action<int>? CounterChangedCallback { get; set; }

    [RelayCommand]
    private void IncrementCounter()
    {
        Counter++;
        CounterChangedCallback?.Invoke(Counter);
    }

    [RelayCommand]
    private void DecrementCounter()
    {
        Counter--;
        CounterChangedCallback?.Invoke(Counter);
    }

    [RelayCommand]
    private void ResetCounter()
    {
        Counter = 0;
        CounterChangedCallback?.Invoke(Counter);
    }
}
