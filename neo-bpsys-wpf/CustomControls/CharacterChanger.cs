using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace neo_bpsys_wpf.CustomControls
{
    /* 
     * CharacterChanger 自定义控件
     * 用于实现可交互的字符切换功能，支持索引管理、命令绑定和间距调整
     * 
     * 依赖属性：
     * - Index: 当前选中字符的索引位置
     * - Command: 索引变化时触发的命令
     * - Spacing: 字符元素之间的间隔距离
     */
    public class CharacterChanger : Control
    {
        /* 
         * 静态构造函数
         * 覆盖默认样式键元数据，确保使用CharacterChanger的样式模板
         */
        static CharacterChanger()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(CharacterChanger),
                new FrameworkPropertyMetadata(typeof(CharacterChanger))
            );
        }

        /* 
         * IndexProperty 依赖属性
         * 存储当前选中字符的索引值，默认值为0
         * 类型：int
         */
        public static readonly DependencyProperty IndexProperty = DependencyProperty.Register(
            "Index",
            typeof(int),
            typeof(CharacterChanger),
            new PropertyMetadata(0)
        );

        /* 
         * 获取或设置当前字符索引值
         * 值范围由具体应用场景决定
         */
        public int Index
        {
            get { return (int)GetValue(IndexProperty); }
            set { SetValue(IndexProperty, value); }
        }

        /* 
         * CommandProperty 依赖属性
         * 存储可执行命令对象，当字符索引变化时触发
         * 类型：ICommand，可为空
         */
        public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
            "Command",
            typeof(ICommand),
            typeof(CharacterChanger),
            new PropertyMetadata(null)
        );

        /* 
         * 获取或设置索引变化时触发的命令
         * 需要实现ICommand接口的对象
         */
        public ICommand Command
        {
            get { return (ICommand)GetValue(CommandProperty); }
            set { SetValue(CommandProperty, value); }
        }

        /* 
         * SpacingProperty 依赖属性
         * 存储字符元素之间的间隔距离值，默认值为0.0
         * 类型：double
         */
        public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
            "Spacing",
            typeof(double),
            typeof(CharacterChanger),
            new PropertyMetadata(0.0)
        );

        /* 
         * 获取或设置字符之间的间隔距离
         * 有效值范围为大于等于0的浮点数
         */
        public double Spacing
        {
            get { return (double)GetValue(SpacingProperty); }
            set { SetValue(SpacingProperty, value); }
        }
    }
}