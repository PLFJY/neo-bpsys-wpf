using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using neo_bpsys_wpf.Enums;

namespace neo_bpsys_wpf.Converters
{
    public class StringToGameActionConverter : IValueConverter
    {
        private static readonly Dictionary<string, GameAction> ActionDict = new(StringComparer.OrdinalIgnoreCase)
        {
            ["None"] = GameAction.None,
            ["BanMap"] = GameAction.BanMap,
            ["PickMap"] = GameAction.PickMap,
            ["PickCamp"] = GameAction.PickCamp,
            ["BanSur"] = GameAction.BanSur,
            ["BanHun"] = GameAction.BanHun,
            ["PickSur"] = GameAction.PickSur,
            ["PickHun"] = GameAction.PickHun
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 处理空值和类型错误
            if (value is not string stringValue) return GameAction.None;

            // 不区分大小写查找
            return ActionDict.TryGetValue(stringValue, out var action)
                ? action
                : GameAction.None;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 反向转换实现（可选）
            if (value is not GameAction action) return null;
            foreach (var pair in ActionDict)
            {
                if (pair.Value == action) return pair.Key;
            }
            return null;
        }
    }
}