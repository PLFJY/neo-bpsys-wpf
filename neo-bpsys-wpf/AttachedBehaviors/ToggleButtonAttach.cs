using System.Windows;
using System.Windows.Controls.Primitives;

namespace neo_bpsys_wpf.AttachedBehaviors;

/// <summary>
/// 为 <see cref="ToggleButton"/> 提供自动折叠行为的附加属性。
/// </summary>
public static class ToggleButtonAttach
{
    #region IsAutoFold

    /// <summary>
    /// 获取指定 <see cref="ToggleButton"/> 的 <see cref="IsAutoFoldProperty"/> 附加属性值。
    /// </summary>
    /// <param name="control">要获取属性值的 <see cref="ToggleButton"/>。</param>
    /// <returns>如果启用了自动折叠则为 <c>true</c>，否则为 <c>false</c>。</returns>
    [AttachedPropertyBrowsableForType(typeof(ToggleButton))]
    public static bool GetIsAutoFold(ToggleButton control)
    {
        return (bool)control.GetValue(IsAutoFoldProperty);
    }

    /// <summary>
    /// 设置指定 <see cref="ToggleButton"/> 的 <see cref="IsAutoFoldProperty"/> 附加属性值。
    /// </summary>
    /// <param name="control">要设置属性值的 <see cref="ToggleButton"/>。</param>
    /// <param name="value">是否启用自动折叠。</param>
    public static void SetIsAutoFold(ToggleButton control, bool value)
    {
        control.SetValue(IsAutoFoldProperty, value);
    }

    /// <summary>
    /// 为具有 ToggleButtonGorgeousThemeSwitchStyle 样式的 <see cref="ToggleButton"/> 设置是否启用自动折叠
    /// </summary>
    public static readonly DependencyProperty IsAutoFoldProperty =
        DependencyProperty.RegisterAttached(
            "IsAutoFold",
            typeof(bool),
            typeof(ToggleButtonAttach),
            new PropertyMetadata(false, ToggleButtonChanged)
        );

    private static void ToggleButtonChanged(
        DependencyObject o,
        DependencyPropertyChangedEventArgs e
    )
    {
        if (o is not ToggleButton control)
            return;
        if ((bool)e.NewValue)
        {
            control.MouseLeave += Control_MouseLeave;
            control.Checked += Control_Checked;
            control.Unchecked += Control_Checked;
            if (!control.IsMouseOver)
                VisualStateManager.GoToState(
                    control,
                    control.IsChecked == true ? "MouseLeaveChecked" : "MouseLeaveUnChecked",
                    false
                );
        }
        else
        {
            control.MouseLeave -= Control_MouseLeave;
            control.Checked -= Control_Checked;
            control.Unchecked -= Control_Checked;
            VisualStateManager.GoToState(control, "MouseOver", false);
        }
    }

    private static void Control_Checked(object sender, RoutedEventArgs e)
    {
        var control = (ToggleButton)sender;
        if (control.IsMouseOver)
            return;
        VisualStateManager.GoToState(
            control,
            control.IsChecked == true ? "MouseLeaveChecked" : "MouseLeaveUnChecked",
            false
        );
    }

    private static async void Control_MouseLeave(
        object sender,
        System.Windows.Input.MouseEventArgs e
    )
    {
        await Task.Delay(1000);
        var control = (ToggleButton)sender;
        VisualStateManager.GoToState(
            control,
            control.IsChecked == true ? "MouseLeaveChecked" : "MouseLeaveUnChecked",
            false
        );
    }

    #endregion
}