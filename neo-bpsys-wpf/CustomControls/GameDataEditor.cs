using System.Windows;
using System.Windows.Controls;

namespace neo_bpsys_wpf.CustomControls
{
    /// <summary>
    /// 自定义控件基类，用于统一管理游戏数据编辑相关的属性和行为。
    /// 提供数据值和数据类型两个可绑定属性，支持样式、动画等WPF特性。
    /// </summary>
    public class GameDataEditor : Control
    {
        /// <summary>
        /// 获取或设置当前游戏数据的值内容。
        /// 支持通过依赖属性系统进行数据绑定、样式设置及动画操作。
        /// </summary>
        public string GameDataValue
        {
            get { return (string)GetValue(GameDataValueProperty); }
            set { SetValue(GameDataValueProperty, value); }
        }

        /// <summary>
        /// 标识GameDataValue依赖属性的注册元数据。
        /// 提供属性变更通知和默认值支持（初始值为null）。
        /// </summary>
        public static readonly DependencyProperty GameDataValueProperty =
            DependencyProperty.Register(
                "GameDataValue",
                typeof(string),
                typeof(GameDataEditor),
                new PropertyMetadata(null)
            );

        /// <summary>
        /// 获取或设置当前游戏数据的类型标识。
        /// 支持通过依赖属性系统进行数据绑定、样式设置及动画操作。
        /// </summary>
        public string GameDataType
        {
            get { return (string)GetValue(GameDataTypeProperty); }
            set { SetValue(GameDataTypeProperty, value); }
        }

        /// <summary>
        /// 标识GameDataType依赖属性的注册元数据。
        /// 提供属性变更通知和默认值支持（初始值为null）。
        /// </summary>
        public static readonly DependencyProperty GameDataTypeProperty =
            DependencyProperty.Register(
                "GameDataType",
                typeof(string),
                typeof(GameDataEditor),
                new PropertyMetadata(null)
            );
    }
}