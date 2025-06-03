using neo_bpsys_wpf.Enums;
using System.Windows;

namespace neo_bpsys_wpf.Services
{
    public interface IFrontService
    {
        Dictionary<Type, bool> FrontWindowStates { get; }
        double GlobalScoreTotalMargin { get; set; }

        /// <summary>
        /// 从JSON中加载窗口中元素的位置信息
        /// </summary>
        /// <typeparam name="T">窗口类型</typeparam>
        /// <param name="canvasName">画布名称</param>
        Task LoadWindowElementsPositionAsync<T>(string canvasName = "BaseCanvas") where T : Window;
        void AllWindowHide();
        void AllWindowShow();
        void ShowWindow<T>() where T : Window;
        void HideWindow<T>() where T : Window;
        /// <summary>
        /// 还原窗口中的元素到初始位置
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="canvasName"></param>
        void RestoreInitialPositions<T>(string canvasName = "BaseCanvas") where T : Window;
        /// <summary>
        /// 获取窗口中元素的位置信息
        /// </summary>
        /// <typeparam name="T">窗口类型</typeparam>
        /// <param name="canvasName">画布名称</param>
        void SaveWindowElementsPosition<T>(string canvasName = "BaseCanvas") where T : Window;
        /// <summary>
        /// 设置分数统计
        /// </summary>
        /// <param name="Team"></param>
        /// <param name="gameProgress"></param>
        /// <param name="camp"></param>
        /// <param name="score"></param>
        void SetGlobalScore(string team, GameProgress gameProgress, Camp camp, int score);
        /// <summary>
        /// 重置分数统计为横杠
        /// </summary>
        /// <param name="Team"></param>
        /// <param name="gameProgress"></param>
        void SetGlobalScoreToBar(string team, GameProgress gameProgress);
        /// <summary>
        /// 重置全局分数统计
        /// </summary>
        void ResetGlobalScore();
        void SwitchGameType(bool isBo3);
    }
}