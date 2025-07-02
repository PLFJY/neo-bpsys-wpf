using neo_bpsys_wpf.Enums;
using System.Windows;

namespace neo_bpsys_wpf.Abstractions.Services
{
    /// <summary>
    /// 前台窗口接口服务
    /// </summary>
    public interface IFrontService
    {
        /// <summary>
        /// 前台窗口状态
        /// </summary>
        Dictionary<Type, bool> FrontWindowStates { get; }

        /// <summary>
        /// 从JSON中加载窗口中元素的位置信息
        /// </summary>
        /// <typeparam name="T">窗口类型</typeparam>
        /// <param name="canvasName">画布名称</param>
        Task LoadWindowElementsPositionOnStartupAsync<T>(string canvasName = "BaseCanvas") where T : Window;
        /// <summary>
        /// 隐藏所有窗口
        /// </summary>
        void AllWindowHide();
        /// <summary>
        /// 显示所有窗口
        /// </summary>
        void AllWindowShow();
        /// <summary>
        /// 显示窗口
        /// </summary>
        /// <typeparam name="T">窗口类型</typeparam>
        void ShowWindow<T>() where T : Window;
        /// <summary>
        /// 隐藏窗口
        /// </summary>
        /// <typeparam name="T">窗口类型</typeparam>
        void HideWindow<T>() where T : Window;

        /// <summary>
        /// 还原窗口中的元素到初始位置
        /// </summary>
        /// <typeparam name="T">窗口类型</typeparam>
        /// <param name="canvasName">画布名称</param>
        Task RestoreInitialPositions<T>(string canvasName = "BaseCanvas") where T : Window;
        /// <summary>
        /// 获取窗口中元素的位置信息
        /// </summary>
        /// <typeparam name="T">窗口类型</typeparam>
        /// <param name="canvasName">画布名称</param>
        void SaveWindowElementsPosition<T>(string canvasName = "BaseCanvas") where T : Window;
        /// <summary>
        /// 设置分数统计
        /// </summary>
        /// <param name="team">队伍，从<see cref="ISharedDataService"/>里拿nameof</param>
        /// <param name="gameProgress">游戏进度</param>
        /// <param name="camp">阵营</param>
        /// <param name="score">分数</param>
        void SetGlobalScore(string team, GameProgress gameProgress, Camp camp, int score);
        /// <summary>
        /// 重置分数统计为横杠
        /// </summary>
        /// <param name="team">队伍，从<see cref="ISharedDataService"/>里拿nameof</param>
        /// <param name="gameProgress">游戏进度</param>
        void SetGlobalScoreToBar(string team, GameProgress gameProgress);
        /// <summary>
        /// 重置全局分数统计
        /// </summary>
        void ResetGlobalScore();

        /// <summary>
        /// 渐入动画
        /// </summary>
        /// <param name="controlNameHeader">控件名称头</param>
        /// <param name="controlIndex">控件索引</param>
        /// <param name="controlNameFooter">控件名称尾</param>
        /// <typeparam name="T">窗口类型</typeparam>
        void FadeInAnimation<T>(string controlNameHeader, int controlIndex, string controlNameFooter) where T : Window;
        /// <summary>
        /// 渐出动画
        /// </summary>
        /// <param name="controlNameHeader">控件名称头</param>
        /// <param name="controlIndex">控件索引</param>
        /// <param name="controlNameFooter">控件名称尾</param>
        /// <typeparam name="T">窗口类型</typeparam>
        void FadeOutAnimation<T>(string controlNameHeader, int controlIndex, string controlNameFooter) where T : Window;
        /// <summary>
        /// 呼吸动画
        /// </summary>
        /// <param name="controlNameHeader">控件名称头</param>
        /// <param name="controlIndex">控件索引</param>
        /// <param name="controlNameFooter">控件名称尾</param>
        /// <typeparam name="T">窗口类型</typeparam>
        Task BreathingStart<T>(string controlNameHeader, int controlIndex, string controlNameFooter) where T : Window;
        /// <summary>
        /// 停止呼吸动画
        /// </summary>
        /// <param name="controlNameHeader">控件名称头</param>
        /// <param name="controlIndex">控件索引</param>
        /// <param name="controlNameFooter">控件名称尾</param>
        /// <typeparam name="T">窗口类型</typeparam>
        /// <returns></returns>
        Task BreathingStop<T>(string controlNameHeader, int controlIndex, string controlNameFooter) where T : Window;

        /// <summary>
        /// 应用设置
        /// </summary>
        /// <param name="windowType">窗口类型</param>
        /// <param name="isInitial">是否是初始化模式</param>
        void ApplySettings(Type windowType, bool isInitial = false);
        /// <summary>
        /// 应用所有窗口设置
        /// </summary>
        void ApplyAllWindowsSettings();
    }
}