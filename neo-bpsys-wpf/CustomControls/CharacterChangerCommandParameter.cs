using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace neo_bpsys_wpf.CustomControls
{
    /// <summary>
    /// 表示字符更改命令的参数容器
    /// 用于封装目标索引和源按钮内容两个关键参数
    /// </summary>
    public class CharacterChangerCommandParameter
    {
        /// <summary>
        /// 获取或设置目标位置的索引值
        /// 通常用于标识需要修改的目标位置序号
        /// </summary>
        public int Target { get; set; }

        /// <summary>
        /// 获取或设置源按钮的内容标识
        /// 通常用于存储触发修改操作的按钮关联数据
        /// </summary>
        public int Source { get; set; }

        /// <summary>
        /// 初始化CharacterChangerCommandParameter的新实例
        /// </summary>
        /// <param name="index">目标索引值</param>
        /// <param name="buttonContent">源按钮内容标识</param>
        /// <remarks>
        /// 该构造函数用于初始化目标索引和源按钮内容两个核心参数
        /// 参数对应关系：
        /// - index → Target属性
        /// - buttonContent → Source属性
        /// </remarks>
        public CharacterChangerCommandParameter(int index, int buttonContent)
        {
            Target = index;
            Source = buttonContent;
        }
    }
}