using System.Windows.Media;

namespace neo_bpsys_wpf.Helpers
{
    internal static class FontsHelper
    {

        /// <summary>
        /// 获取系统已安装的字体集合
        /// </summary>
        /// <returns>字体名称到FontFamily的映射字典</returns>
        public static List<FontFamily> GetSystemFonts()
        {
            var fontList = new List<FontFamily>();

            // 遍历系统字体集合
            foreach (FontFamily fontFamily in Fonts.SystemFontFamilies)
            {
                try
                {
                    // 避免重复添加相同名称的字体
                    if (!fontList.Contains(fontFamily))
                        fontList.Add(fontFamily);
                }
                catch
                {
                    // 忽略无法处理的字体
                }
            }

            return fontList;
        }
    }
}