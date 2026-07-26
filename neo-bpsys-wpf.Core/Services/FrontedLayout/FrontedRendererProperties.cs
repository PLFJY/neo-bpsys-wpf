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

    /// <summary>
    /// 行为系统用于解析动画目标的控件标识。
    /// </summary>
    public static readonly DependencyProperty BehaviorGuidProperty =
        DependencyProperty.RegisterAttached(
            "BehaviorGuid",
            typeof(Guid),
            typeof(FrontedRendererProperties),
            new PropertyMetadata(Guid.Empty));

    /// <summary>
    /// 获取行为系统控件标识。
    /// </summary>
    public static Guid GetBehaviorGuid(DependencyObject element)
    {
        return (Guid)element.GetValue(BehaviorGuidProperty);
    }

    /// <summary>
    /// 设置行为系统控件标识。
    /// </summary>
    public static void SetBehaviorGuid(DependencyObject element, Guid value)
    {
        element.SetValue(BehaviorGuidProperty, value);
    }

    /// <summary>
    /// 标记元素是否为行为动画 runtime 生成的辅助视觉层。
    /// </summary>
    public static readonly DependencyProperty IsAnimationAuxiliaryElementProperty =
        DependencyProperty.RegisterAttached(
            "IsAnimationAuxiliaryElement",
            typeof(bool),
            typeof(FrontedRendererProperties),
            new PropertyMetadata(false));

    /// <summary>
    /// 获取元素是否为行为动画 runtime 生成的辅助视觉层。
    /// </summary>
    public static bool GetIsAnimationAuxiliaryElement(DependencyObject element)
    {
        return (bool)element.GetValue(IsAnimationAuxiliaryElementProperty);
    }

    /// <summary>
    /// 设置元素是否为行为动画 runtime 生成的辅助视觉层。
    /// </summary>
    public static void SetIsAnimationAuxiliaryElement(DependencyObject element, bool value)
    {
        element.SetValue(IsAnimationAuxiliaryElementProperty, value);
    }

    /// <summary>
    /// 标记表示生成控件主要视觉内容的元素。
    /// </summary>
    public static readonly DependencyProperty IsPrimaryContentElementProperty =
        DependencyProperty.RegisterAttached(
            "IsPrimaryContentElement",
            typeof(bool),
            typeof(FrontedRendererProperties),
            new PropertyMetadata(false));

    /// <summary>
    /// 获取元素是否表示生成控件的主要视觉内容。
    /// </summary>
    /// <param name="element">要读取的元素。</param>
    /// <returns>当元素是主要内容元素时为 <c>true</c>;否则为 <c>false</c>。</returns>
    public static bool GetIsPrimaryContentElement(DependencyObject element)
    {
        return (bool)element.GetValue(IsPrimaryContentElementProperty);
    }

    /// <summary>
    /// 设置元素是否表示生成控件的主要视觉内容。
    /// </summary>
    /// <param name="element">要更新的元素。</param>
    /// <param name="value">元素是否是主要内容元素。</param>
    public static void SetIsPrimaryContentElement(DependencyObject element, bool value)
    {
        element.SetValue(IsPrimaryContentElementProperty, value);
    }

    /// <summary>
    /// 标识拥有动画部分的生成控件的行为 GUID。
    /// </summary>
    public static readonly DependencyProperty ParentBehaviorGuidProperty =
        DependencyProperty.RegisterAttached(
            "ParentBehaviorGuid",
            typeof(Guid),
            typeof(FrontedRendererProperties),
            new PropertyMetadata(Guid.Empty));

    /// <summary>
    /// 获取拥有动画部分的生成控件的行为 GUID。
    /// </summary>
    /// <param name="element">要读取的元素。</param>
    /// <returns>所属控件的行为 GUID。</returns>
    public static Guid GetParentBehaviorGuid(DependencyObject element)
    {
        return (Guid)element.GetValue(ParentBehaviorGuidProperty);
    }

    /// <summary>
    /// 设置拥有动画部分的生成控件的行为 GUID。
    /// </summary>
    /// <param name="element">要更新的元素。</param>
    /// <param name="value">所属控件的行为 GUID。</param>
    public static void SetParentBehaviorGuid(DependencyObject element, Guid value)
    {
        element.SetValue(ParentBehaviorGuidProperty, value);
    }

    /// <summary>
    /// 标识拥有动画部分的生成控件的已注册名称。
    /// </summary>
    public static readonly DependencyProperty ParentRegisteredNameProperty =
        DependencyProperty.RegisterAttached(
            "ParentRegisteredName",
            typeof(string),
            typeof(FrontedRendererProperties),
            new PropertyMetadata(string.Empty));

    /// <summary>
    /// 获取拥有动画部分的生成控件的已注册名称。
    /// </summary>
    /// <param name="element">要读取的元素。</param>
    /// <returns>所属控件的已注册名称。</returns>
    public static string GetParentRegisteredName(DependencyObject element)
    {
        return (string)element.GetValue(ParentRegisteredNameProperty);
    }

    /// <summary>
    /// 设置拥有动画部分的生成控件的已注册名称。
    /// </summary>
    /// <param name="element">要更新的元素。</param>
    /// <param name="value">所属控件的已注册名称。</param>
    public static void SetParentRegisteredName(DependencyObject element, string value)
    {
        element.SetValue(ParentRegisteredNameProperty, value);
    }

    /// <summary>
    /// 标识生成辅助元素的稳定动画部分名称。
    /// </summary>
    public static readonly DependencyProperty AnimationPartNameProperty =
        DependencyProperty.RegisterAttached(
            "AnimationPartName",
            typeof(string),
            typeof(FrontedRendererProperties),
            new PropertyMetadata(string.Empty));

    /// <summary>
    /// 获取生成辅助元素的稳定动画部分名称。
    /// </summary>
    /// <param name="element">要读取的元素。</param>
    /// <returns>稳定的动画部分名称。</returns>
    public static string GetAnimationPartName(DependencyObject element)
    {
        return (string)element.GetValue(AnimationPartNameProperty);
    }

    /// <summary>
    /// 设置生成辅助元素的稳定动画部分名称。
    /// </summary>
    /// <param name="element">要更新的元素。</param>
    /// <param name="value">稳定的动画部分名称。</param>
    public static void SetAnimationPartName(DependencyObject element, string value)
    {
        element.SetValue(AnimationPartNameProperty, value);
    }

    /// <summary>
    /// 标识用于解析生成部分的百分比尺寸和偏移的父控件。
    /// </summary>
    public static readonly DependencyProperty AnimationPartParentProperty =
        DependencyProperty.RegisterAttached(
            "AnimationPartParent",
            typeof(FrameworkElement),
            typeof(FrontedRendererProperties),
            new PropertyMetadata(null));

    /// <summary>
    /// 获取生成动画部分所使用的父控件。
    /// </summary>
    /// <param name="element">要读取的元素。</param>
    /// <returns>所属控件根,或 <c>null</c>。</returns>
    public static FrameworkElement? GetAnimationPartParent(DependencyObject element)
    {
        return (FrameworkElement?)element.GetValue(AnimationPartParentProperty);
    }

    /// <summary>
    /// 设置生成动画部分所使用的父控件。
    /// </summary>
    /// <param name="element">要更新的元素。</param>
    /// <param name="value">所属控件根。</param>
    public static void SetAnimationPartParent(DependencyObject element, FrameworkElement? value)
    {
        element.SetValue(AnimationPartParentProperty, value);
    }
}
