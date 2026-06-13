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
    /// Identifies the behavior GUID of the generated control that owns an animation part.
    /// </summary>
    public static readonly DependencyProperty ParentBehaviorGuidProperty =
        DependencyProperty.RegisterAttached(
            "ParentBehaviorGuid",
            typeof(Guid),
            typeof(FrontedRendererProperties),
            new PropertyMetadata(Guid.Empty));

    /// <summary>
    /// Gets the behavior GUID of the generated control that owns an animation part.
    /// </summary>
    /// <param name="element">The element to read.</param>
    /// <returns>The owning control behavior GUID.</returns>
    public static Guid GetParentBehaviorGuid(DependencyObject element)
    {
        return (Guid)element.GetValue(ParentBehaviorGuidProperty);
    }

    /// <summary>
    /// Sets the behavior GUID of the generated control that owns an animation part.
    /// </summary>
    /// <param name="element">The element to update.</param>
    /// <param name="value">The owning control behavior GUID.</param>
    public static void SetParentBehaviorGuid(DependencyObject element, Guid value)
    {
        element.SetValue(ParentBehaviorGuidProperty, value);
    }

    /// <summary>
    /// Identifies the registered name of the generated control that owns an animation part.
    /// </summary>
    public static readonly DependencyProperty ParentRegisteredNameProperty =
        DependencyProperty.RegisterAttached(
            "ParentRegisteredName",
            typeof(string),
            typeof(FrontedRendererProperties),
            new PropertyMetadata(string.Empty));

    /// <summary>
    /// Gets the registered name of the generated control that owns an animation part.
    /// </summary>
    /// <param name="element">The element to read.</param>
    /// <returns>The owning control registered name.</returns>
    public static string GetParentRegisteredName(DependencyObject element)
    {
        return (string)element.GetValue(ParentRegisteredNameProperty);
    }

    /// <summary>
    /// Sets the registered name of the generated control that owns an animation part.
    /// </summary>
    /// <param name="element">The element to update.</param>
    /// <param name="value">The owning control registered name.</param>
    public static void SetParentRegisteredName(DependencyObject element, string value)
    {
        element.SetValue(ParentRegisteredNameProperty, value);
    }

    /// <summary>
    /// Identifies the stable animation part name of a generated auxiliary element.
    /// </summary>
    public static readonly DependencyProperty AnimationPartNameProperty =
        DependencyProperty.RegisterAttached(
            "AnimationPartName",
            typeof(string),
            typeof(FrontedRendererProperties),
            new PropertyMetadata(string.Empty));

    /// <summary>
    /// Gets the stable animation part name of a generated auxiliary element.
    /// </summary>
    /// <param name="element">The element to read.</param>
    /// <returns>The stable animation part name.</returns>
    public static string GetAnimationPartName(DependencyObject element)
    {
        return (string)element.GetValue(AnimationPartNameProperty);
    }

    /// <summary>
    /// Sets the stable animation part name of a generated auxiliary element.
    /// </summary>
    /// <param name="element">The element to update.</param>
    /// <param name="value">The stable animation part name.</param>
    public static void SetAnimationPartName(DependencyObject element, string value)
    {
        element.SetValue(AnimationPartNameProperty, value);
    }

    /// <summary>
    /// Identifies the parent control used to resolve percentage sizes and offsets for a generated part.
    /// </summary>
    public static readonly DependencyProperty AnimationPartParentProperty =
        DependencyProperty.RegisterAttached(
            "AnimationPartParent",
            typeof(FrameworkElement),
            typeof(FrontedRendererProperties),
            new PropertyMetadata(null));

    /// <summary>
    /// Gets the parent control used by a generated animation part.
    /// </summary>
    /// <param name="element">The element to read.</param>
    /// <returns>The owning control root, or <c>null</c>.</returns>
    public static FrameworkElement? GetAnimationPartParent(DependencyObject element)
    {
        return (FrameworkElement?)element.GetValue(AnimationPartParentProperty);
    }

    /// <summary>
    /// Sets the parent control used by a generated animation part.
    /// </summary>
    /// <param name="element">The element to update.</param>
    /// <param name="value">The owning control root.</param>
    public static void SetAnimationPartParent(DependencyObject element, FrameworkElement? value)
    {
        element.SetValue(AnimationPartParentProperty, value);
    }
}
