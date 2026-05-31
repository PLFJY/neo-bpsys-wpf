using System.Windows;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// v3 renderer 附加属性。
/// </summary>
public static class FrontedRendererProperties
{
    /// <summary>
    /// 标记控件是否由 v3 renderer 生成。
    /// </summary>
    public static readonly DependencyProperty IsGeneratedControlProperty =
        DependencyProperty.RegisterAttached(
            "IsGeneratedControl",
            typeof(bool),
            typeof(FrontedRendererProperties),
            new PropertyMetadata(false));

    /// <summary>
    /// 获取控件是否由 v3 renderer 生成。
    /// </summary>
    public static bool GetIsGeneratedControl(DependencyObject element)
    {
        return (bool)element.GetValue(IsGeneratedControlProperty);
    }

    /// <summary>
    /// 设置控件是否由 v3 renderer 生成。
    /// </summary>
    public static void SetIsGeneratedControl(DependencyObject element, bool value)
    {
        element.SetValue(IsGeneratedControlProperty, value);
    }

    /// <summary>
    /// v3 renderer 注册到 namescope 的名称。
    /// </summary>
    public static readonly DependencyProperty RegisteredNameProperty =
        DependencyProperty.RegisterAttached(
            "RegisteredName",
            typeof(string),
            typeof(FrontedRendererProperties),
            new PropertyMetadata(string.Empty));

    /// <summary>
    /// 获取 v3 renderer 注册到 namescope 的名称。
    /// </summary>
    public static string GetRegisteredName(DependencyObject element)
    {
        return (string)element.GetValue(RegisteredNameProperty);
    }

    /// <summary>
    /// 设置 v3 renderer 注册到 namescope 的名称。
    /// </summary>
    public static void SetRegisteredName(DependencyObject element, string value)
    {
        element.SetValue(RegisteredNameProperty, value);
    }
}
