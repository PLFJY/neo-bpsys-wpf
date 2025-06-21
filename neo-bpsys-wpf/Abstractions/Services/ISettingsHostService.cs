using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using neo_bpsys_wpf.Models;

namespace neo_bpsys_wpf.Abstractions.Services
{
    public interface ISettingsHostService
    {
        Settings Settings { get; set; }
        /// <summary>
        /// 保存配置
        /// </summary>
        void SaveConfig();
        /// <summary>
        /// 读取配置
        /// </summary>
        void LoadConfig();
        /// <summary>
        /// 重置配置
        /// </summary>
        void ResetConfig();
    }
}
