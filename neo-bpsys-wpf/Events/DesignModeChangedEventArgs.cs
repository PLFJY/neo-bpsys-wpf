/*
 * 事件参数类，用于传递设计模式状态变更信息
 * 继承自系统事件参数基类
 * 主要成员：
 * - IsDesignMode 属性：指示当前是否处于设计模式状态
 */
namespace neo_bpsys_wpf.Events
{
    public class DesignModeChangedEventArgs : EventArgs
    {
        // 存储设计模式状态的私有字段
        private bool isDesignMode;

        /*
         * 获取或设置设计模式状态标志
         * true 表示处于设计模式
         * false 表示处于运行时模式
         */
        public bool IsDesignMode
        {
            get { return isDesignMode; }
            set { isDesignMode = value; }
        }
    }
}