﻿using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;

namespace neo_bpsys_wpf.Core.Abstractions.ViewModels;

public abstract partial class ViewModelBase : ObservableRecipient
{
    public ViewModelBase()
    {
        IsActive = true;
    }
    
    /// <summary>
    /// 设置属性并执行回调
    /// </summary>
    /// <param name="field">私有字段引用</param>
    /// <param name="value">新值</param>
    /// <param name="onChanged">属性改变回调</param>
    /// <param name="propertyName">属性名</param>
    /// <typeparam name="T">属性类型</typeparam>
    /// <returns></returns>
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