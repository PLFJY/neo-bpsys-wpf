using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;

/// <summary>
/// 文本绑定表达式中的一个有序源。
/// </summary>
public sealed class FrontedBindingSourceConfig : INotifyPropertyChanged
{
    private string _path = string.Empty;

    /// <summary>
    /// 相对于 <see cref="Abstractions.Services.ISharedDataService"/> 的绑定路径。
    /// </summary>
    public string Path
    {
        get => _path;
        set
        {
            if (_path != value)
            {
                _path = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 可选的、仅供设计器使用的显示名称。
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// 保留的按源格式。
    /// </summary>
    public string? Format { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
