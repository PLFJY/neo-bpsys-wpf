using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Models;
using neo_bpsys_wpf.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters
{
    /// <summary>
    /// 将Score对象转换为包含队伍名称和比赛数据的格式化字符串的值转换器
    /// 用于在WPF界面中显示主队/客队的比赛成绩信息
    /// </summary>
    public class ScoreToStringConverterInBg : IValueConverter
    {
        /// <summary>
        /// 将Score对象转换为格式化字符串
        /// </summary>
        /// <param name="value">待转换的Score对象</param>
        /// <param name="targetType">目标类型（未使用）</param>
        /// <param name="parameter">区分主队/客队的标识符（"Main"或其它值）</param>
        /// <param name="culture">本地化信息（未使用）</param>
        /// <returns>包含队伍名称和比赛数据的格式化字符串，或Binding.DoNothing</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not Score score) return Binding.DoNothing;

            var team = parameter.ToString();

            // 根据队伍类型生成对应的显示信息：
            // 1. 从依赖注入服务获取队伍名称
            // 2. 格式化显示胜负场次和小比分数据
            // 3. 区分主队（Main）和客队（非Main）的显示格式
            if (team == "Main")
            {
                return $"{App.Services.GetRequiredService<ISharedDataService>().MainTeam.Name} W:{score.Win} D:{score.Tie} 小比分:{score.MinorPoints}";
            }
            else
            {
                return $"{App.Services.GetRequiredService<ISharedDataService>().AwayTeam.Name} W:{score.Win} D:{score.Tie} 小比分:{score.MinorPoints}";
            }
        }

        /// <summary>
        /// 不支持反向转换，抛出未实现异常
        /// </summary>
        /// <param name="value">目标值（未使用）</param>
        /// <param name="targetType">目标类型（未使用）</param>
        /// <param name="parameter">转换参数（未使用）</param>
        /// <param name="culture">本地化信息（未使用）</param>
        /// <returns>无返回值，始终抛出异常</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}