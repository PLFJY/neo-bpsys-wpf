using CommunityToolkit.Mvvm.ComponentModel;
using hyjiacan.py4n;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Helpers;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace neo_bpsys_wpf.Models;

/// <summary>
/// 角色模型类，表示游戏中的角色实体对象
/// 继承自ObservableObject以支持MVVM模式下的属性通知
/// 包含角色基本信息、阵营标识、图像资源及拼音辅助字段
/// </summary>
public class Character : ObservableObject
{
    /// <summary>
    /// 获取或设置角色名称（默认空字符串）
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 获取角色所属阵营（只读属性，初始化后不可变）
    /// </summary>
    public Camp Camp { get; }

    /// <summary>
    /// 获取或设置角色图像文件名（默认空字符串）
    /// </summary>
    public string ImageFileName { get; set; } = string.Empty;

    /// <summary>
    /// 大尺寸角色图像资源（反序列化时忽略）
    /// 通过ImageSourceKey.surBig/hunBig加载
    /// </summary>
    [JsonIgnore]
    public ImageSource? BigImage { get; set; }

    /// <summary>
    /// 头像尺寸角色图像资源（反序列化时忽略）
    /// 通过ImageSourceKey.surHeader/hunHeader加载
    /// </summary>
    [JsonIgnore]
    public ImageSource? HeaderImage { get; set; }

    /// <summary>
    /// 单色头像图像资源（反序列化时忽略）
    /// 通过ImageSourceKey.surHeader_singleColor/hunHeader_singleColor加载
    /// </summary>
    [JsonIgnore]
    public ImageSource? HeaderImage_SingleColor { get; set; }

    /// <summary>
    /// 半身像图像资源（反序列化时忽略）
    /// 通过ImageSourceKey.surHalf/hunHalf加载
    /// </summary>
    [JsonIgnore]
    public ImageSource? HalfImage { get; set; }

    /// <summary>
    /// 角色名称全拼（默认空字符串）
    /// 使用WITH_U_AND_COLON格式存储（如："l:ian"表示"lian"）
    /// </summary>
    public string FullSpell { get; set; } = string.Empty;

    /// <summary>
    /// 角色名称简拼（默认空字符串）
    /// 特殊情况："26号守卫"强制指定为"bb"
    /// </summary>
    public string Abbrev { get; set; } = string.Empty;

    /// <summary>
    /// 完整构造函数，创建角色实例并初始化图像资源与拼音信息
    /// </summary>
    /// <param name="name">角色名称</param>
    /// <param name="camp">角色所属阵营</param>
    /// <param name="imageFileName">角色图像文件名</param>
    public Character(string name, Camp camp, string imageFileName)
    {
        Name = name;
        Camp = camp;
        ImageFileName = imageFileName;

        // 初始化不同尺寸的图像资源
        BigImage = GetImageSource(camp == Camp.Sur ? ImageSourceKey.surBig : ImageSourceKey.hunBig);
        HeaderImage = GetImageSource(camp == Camp.Sur ? ImageSourceKey.surHeader : ImageSourceKey.hunHeader);
        HeaderImage_SingleColor = GetImageSource(camp == Camp.Sur ? ImageSourceKey.surHeader_singleColor : ImageSourceKey.hunHeader_singleColor);
        HalfImage = GetImageSource(camp == Camp.Sur ? ImageSourceKey.surHalf : ImageSourceKey.hunHalf);

        // 拼音处理模块
        var format = PinyinFormat.WITHOUT_TONE | PinyinFormat.LOWERCASE | PinyinFormat.WITH_U_AND_COLON | PinyinFormat.WITH_V;
        var pinyin = Pinyin4Net.GetPinyin(name, format);
        var parts = pinyin.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);

        // 特殊名称修正逻辑
        if (name.StartsWith("调"))
            parts[0] = "tiao";

        // 生成全拼字符串
        FullSpell = string.Concat(parts);

        // 生成简拼字符串（特殊案例单独处理）
        if (name.Equals("26号守卫"))
        {
            Abbrev = "bb";
        }
        else
        {
            Abbrev = string.Concat(parts.Select(p => p[0]));
        }
    }

    /// <summary>
    /// 简化构造函数，仅初始化阵营属性
    /// 用于创建默认角色实例
    /// </summary>
    /// <param name="camp">角色所属阵营</param>
    public Character(Camp camp)
    {
        Camp = camp;
    }

    /// <summary>
    /// 根据图像资源键获取对应图像源
    /// </summary>
    /// <param name="key">图像资源标识符</param>
    /// <returns>图像源对象或null</returns>
    private ImageSource? GetImageSource(ImageSourceKey key)
    {
        return ImageHelper.GetImageSourceFromFileName(key, ImageFileName);
    }
}