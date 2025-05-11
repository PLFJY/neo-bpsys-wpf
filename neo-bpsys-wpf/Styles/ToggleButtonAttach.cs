using System.Windows;
using System.Windows.Controls.Primitives;

namespace neo_bpsys_wpf.Styles
{
    /// <summary>
    /// 为 ToggleButton 提供附加属性扩展功能的静态类
    /// </summary>
    public static class ToggleButtonAttach
    {
        #region IsAutoFold
        /// <summary>
        /// 获取控件的自动折叠状态
        /// </summary>
        /// <param name="control">目标 ToggleButton 控件</param>
        /// <returns>当前是否启用自动折叠</returns>
        [AttachedPropertyBrowsableForType(typeof(ToggleButton))]
        public static bool GetIsAutoFold(ToggleButton control)
        {
            return (bool)control.GetValue(IsAutoFoldProperty);
        }

        /// <summary>
        /// 设置控件的自动折叠状态
        /// </summary>
        /// <param name="control">目标 ToggleButton 控件</param>
        /// <param name="value">是否启用自动折叠</param>
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

        /// <summary>
        /// 处理 IsAutoFold 属性变更事件
        /// </summary>
        /// <remarks>
        /// 根据新值动态订阅/取消订阅控件事件，并设置初始视觉状态
        /// </remarks>
        private static void ToggleButtonChanged(
            DependencyObject o,
            DependencyPropertyChangedEventArgs e
        )
        {
            if (o is not ToggleButton control)
                return;
            if ((bool)e.NewValue)
            {
                // 启用自动折叠时订阅相关事件
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
                // 禁用自动折叠时取消事件订阅并重置状态
                control.MouseLeave -= Control_MouseLeave;
                control.Checked -= Control_Checked;
                control.Unchecked -= Control_Checked;
                VisualStateManager.GoToState(control, "MouseOver", false);
            }
        }

        /// <summary>
        /// 处理选中状态变更事件
        /// </summary>
        /// <remarks>
        /// 当鼠标不在控件上方时，根据选中状态切换视觉状态
        /// </remarks>
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

        /// <summary>
        /// 处理鼠标离开事件（带1000ms延迟）
        /// </summary>
        /// <remarks>
        /// 延迟后根据当前选中状态更新视觉状态，防止误触发
        /// </remarks>
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
}