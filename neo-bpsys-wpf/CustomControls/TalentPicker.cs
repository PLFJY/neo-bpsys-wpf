using System.Windows;
using System.Windows.Controls;

namespace neo_bpsys_wpf.CustomControls
{
    /// <summary>
    /// 自定义控件TalentPicker用于管理角色天赋选择状态，支持时钟方向布局的交互式选择。
    /// 12, 3, 6, 9 分别对应天赋图在时钟上的四个方向：
    ///    12
    ///  9     3
    ///    6
    /// 提供双向绑定的选中状态属性和角色类型标识属性。
    /// </summary>
    public class TalentPicker : Control
    {
        /// <summary>
        /// 获取或设置当前角色是否为监管者
        /// 影响天赋图的渲染样式和交互逻辑
        /// </summary>
        public bool IsTypeHun
        {
            get { return (bool)GetValue(IsTypeHunProperty); }
            set { SetValue(IsTypeHunProperty, value); }
        }

        /// <summary>
        /// 标识IsTypeHun依赖属性的元数据信息
        /// 默认值为false，支持动画、样式和数据绑定
        /// </summary>
        public static readonly DependencyProperty IsTypeHunProperty = DependencyProperty.Register(
            "IsTypeHun",
            typeof(bool),
            typeof(TalentPicker),
            new PropertyMetadata(false)
        );

        /// <summary>
        /// 获取或设置当前关联角色的名称
        /// 用于界面显示和数据上下文关联
        /// </summary>
        public string CharacterName
        {
            get { return (string)GetValue(CharacterNameProperty); }
            set { SetValue(CharacterNameProperty, value); }
        }

        /// <summary>
        /// 标识CharacterName依赖属性的元数据信息
        /// 默认值为空字符串，支持动画、样式和数据绑定
        /// </summary>
        public static readonly DependencyProperty CharacterNameProperty =
            DependencyProperty.Register("CharacterName", typeof(string), typeof(TalentPicker), new PropertyMetadata(string.Empty));

        /// <summary>
        /// 获取或设置12点方向天赋节点的选中状态
        /// 支持双向绑定，影响界面交互反馈
        /// </summary>
        public bool Is12Checked
        {
            get { return (bool)GetValue(Is12CheckedProperty); }
            set { SetValue(Is12CheckedProperty, value); }
        }

        /// <summary>
        /// 标识Is12Checked依赖属性的元数据信息
        /// 默认值为false，启用双向绑定支持
        /// </summary>
        public static readonly DependencyProperty Is12CheckedProperty =
            DependencyProperty.Register("Is12Checked", typeof(bool), typeof(TalentPicker), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        /// <summary>
        /// 获取或设置3点方向天赋节点的选中状态
        /// 支持双向绑定，影响界面交互反馈
        /// </summary>
        public bool Is3Checked
        {
            get { return (bool)GetValue(Is3CheckedProperty); }
            set { SetValue(Is3CheckedProperty, value); }
        }

        /// <summary>
        /// 标识Is3Checked依赖属性的元数据信息
        /// 默认值为false，启用双向绑定支持
        /// </summary>
        public static readonly DependencyProperty Is3CheckedProperty =
            DependencyProperty.Register("Is3Checked", typeof(bool), typeof(TalentPicker), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));


        /// <summary>
        /// 获取或设置6点方向天赋节点的选中状态
        /// 支持双向绑定，影响界面交互反馈
        /// </summary>
        public bool Is6Checked
        {
            get { return (bool)GetValue(Is6CheckedProperty); }
            set { SetValue(Is6CheckedProperty, value); }
        }

        /// <summary>
        /// 标识Is6Checked依赖属性的元数据信息
        /// 默认值为false，启用双向绑定支持
        /// </summary>
        public static readonly DependencyProperty Is6CheckedProperty =
            DependencyProperty.Register("Is6Checked", typeof(bool), typeof(TalentPicker), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));


        /// <summary>
        /// 获取或设置9点方向天赋节点的选中状态
        /// 支持双向绑定，影响界面交互反馈
        /// </summary>
        public bool Is9Checked
        {
            get { return (bool)GetValue(Is9CheckedProperty); }
            set { SetValue(Is9CheckedProperty, value); }
        }

        /// <summary>
        /// 标识Is9Checked依赖属性的元数据信息
        /// 默认值为false，启用双向绑定支持
        /// </summary>
        public static readonly DependencyProperty Is9CheckedProperty =
            DependencyProperty.Register("Is9Checked", typeof(bool), typeof(TalentPicker), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    }
}