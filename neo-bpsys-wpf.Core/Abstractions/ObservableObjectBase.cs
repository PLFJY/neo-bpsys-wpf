using CommunityToolkit.Mvvm.ComponentModel;
using System.Runtime.CompilerServices;

namespace neo_bpsys_wpf.Core.Abstractions;

/// <summary>
/// 实现 INotifyPropertyChanged 并提供属性变更处理助手方法的基类。
/// 不特定于 MVVM 层。
/// </summary>
public abstract class ObservableObjectBase : ObservableRecipient
{
    /// <summary>
    /// 设置属性并执行回调
    /// </summary>
    /// <param name="field">私有字段引用</param>
    /// <param name="value">新值</param>
    /// <param name="onChanged">属性改变回调</param>
    /// <param name="propertyName">属性名</param>
    /// <typeparam name="T">属性类型</typeparam>
    /// <returns>当属性值发生改变时返回 true，否则返回 false</returns>
    protected bool SetPropertyWithAction<T>(ref T field, T value, Action<T>? onChanged = null,
        [CallerMemberName] string? propertyName = null)
    {
        var oldValue = field;
        if (!SetProperty(ref field, value, propertyName))
            return false;

        onChanged?.Invoke(oldValue);
        return true;
    }
}