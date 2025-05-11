using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace neo_bpsys_wpf.Extensions
{
    /// <summary>
    /// 提供对ObservableCollection<T>类型的扩展方法
    /// </summary>
    public static class ObservableCollectionExtensions
    {
        /// <summary>
        /// 将指定集合的元素批量添加到目标ObservableCollection中
        /// </summary>
        /// <typeparam name="T">集合元素类型</typeparam>
        /// <param name="collection">要扩展的目标ObservableCollection对象</param>
        /// <param name="items">包含要添加元素的IEnumerable集合</param>
        /// <returns>此方法不返回值</returns>
        public static void AddRange<T>(this ObservableCollection<T> collection, IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                collection.Add(item);
            }
        }
    }
}