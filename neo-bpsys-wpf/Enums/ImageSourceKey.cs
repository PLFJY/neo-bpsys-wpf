namespace neo_bpsys_wpf.Enums
{
    /// <summary>
    /// 定义图像资源标识符的枚举类型，用于统一管理系统中不同场景的图像资源键值。
    /// 枚举成员命名遵循"用途_显示状态"的命名规范，用于标识：
    /// - bpui: 基础UI元素图像
    /// - hun/sur/map等: 监管者/求生者/地图的图像资源
    /// - Big/Half/Header: 图像尺寸规格
    /// - singleColor: 单色样式标识
    /// - talent: 天赋图像
    /// - trait: 监管者辅助特质图像
    /// </summary>
    public enum ImageSourceKey
    {
        bpui,
        hunBig,
        hunHalf,
        hunHeader,
        hunHeader_singleColor,
        map,
        map_singleColor,
        surBig,
        surHalf,
        surHeader,
        surHeader_singleColor,
        talent,
        trait,
    }
}