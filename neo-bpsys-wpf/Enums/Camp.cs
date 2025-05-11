using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Enums;

/// <summary>
/// 表示系统中的阵营类型枚举
/// 该枚举定义了角色类型分类
/// 通过JsonStringEnumConverter实现JSON序列化时保持字符串形式
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Camp
{
    /// <summary>
    /// 求生者标识
    /// </summary>
    Sur,

    /// <summary>
    /// 监管者标识
    /// </summary>
    Hun,
}