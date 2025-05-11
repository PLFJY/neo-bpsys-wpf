using System.Windows;

namespace neo_bpsys_wpf.Services
{
    /// <summary>
    /// 前端服务接口，定义窗口管理和元素布局相关的操作规范
    /// </summary>
    public interface IFrontService
    {
        /// <summary>
        /// 显示所有关联的窗口界面
        /// </summary>
        public void AllWindowShow();

        /// <summary>
        /// 隐藏所有关联的窗口界面
        /// </summary>
        public void AllWindowHide();

        /// <summary>
        /// 显示指定类型的窗口
        /// </summary>
        /// <typeparam name="T">要显示的窗口类型，必须继承自System.Windows.Window</typeparam>
        public void ShowWindow<T>()
            where T : Window;

        /// <summary>
        /// 隐藏指定类型的窗口
        /// </summary>
        /// <typeparam name="T">要隐藏的窗口类型，必须继承自System.Windows.Window</typeparam>
        public void HideWindow<T>()
            where T : Window;

        /// <summary>
        /// 获取指定窗口内画布元素的布局位置信息
        /// </summary>
        /// <param name="window">目标窗口对象</param>
        /// <param name="canvasName">画布名称标识，默认值为"BaseCanvas"</param>
        /// <returns>包含元素位置信息的JSON格式字符串</returns>
        public string GetWindowElementsPosition(Window window, string canvasName = "BaseCanvas");

        /// <summary>
        /// 将保存的位置信息加载到指定窗口的画布元素上
        /// </summary>
        /// <param name="window">目标窗口对象</param>
        /// <param name="json">包含元素位置信息的JSON字符串</param>
        /// <param name="canvasName">画布名称标识，默认值为"BaseCanvas"</param>
        public void LoadWindowElementsPosition(Window window, string json, string canvasName = "BaseCanvas");

        /// <summary>
        /// 将指定窗口的画布元素恢复到初始默认位置
        /// </summary>
        /// <param name="window">目标窗口对象</param>
        /// <param name="canvasName">画布名称标识，默认值为"BaseCanvas"</param>
        public void RestoreInitialPositions(Window window, string canvasName = "BaseCanvas");
    }
}