using System.Text.Json.Serialization;
using System.Windows.Media;
using hyjiacan.py4n;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;

namespace neo_bpsys_wpf.Core.Models;

/// <summary>
/// 角色类, 属性设定均由构造函数完成，不存在后续修改
/// </summary>
public class Character
{
    /// <summary>
    /// 角色名称
    /// </summary>
    public string Name { get; } = string.Empty;

    /// <summary>
    /// 阵营
    /// </summary>
    public Camp Camp { get; }

    /// <summary>
    /// 图片文件名
    /// </summary>
    public string ImageFileName { get; } = string.Empty;

    private ImageSource? _bigImage;

    /// <summary>
    /// 全身立绘
    /// </summary>
    [JsonIgnore]
    public ImageSource? BigImage
    {
        get
        {
            if (_bigImage == null)
            {
                _bigImage = GetImageSource(Camp == Camp.Sur ? ImageSourceKey.surBig : ImageSourceKey.hunBig);
            }

            return _bigImage;
        }
    }

    private ImageSource? _headerImage;

    /// <summary>
    /// 头像图片
    /// </summary>
    [JsonIgnore]
    public ImageSource? HeaderImage
    {
        get
        {
            if (_headerImage == null)
            {
                _headerImage = GetImageSource(Camp == Camp.Sur ? ImageSourceKey.surHeader : ImageSourceKey.hunHeader);
            }

            return _headerImage;
        }
    }

    private ImageSource? _headerImageSingleColor;

    /// <summary>
    /// 黑白头像图片
    /// </summary>
    [JsonIgnore]
    public ImageSource? HeaderImageSingleColor =>
        _headerImageSingleColor ??= GetImageSource(Camp == Camp.Sur
            ? ImageSourceKey.surHeader_singleColor
            : ImageSourceKey.hunHeader_singleColor);

    private ImageSource? _halfImage;

    /// <summary>
    /// 半身立绘
    /// </summary>
    [JsonIgnore]
    public ImageSource? HalfImage =>
        _halfImage ??= GetImageSource(Camp == Camp.Sur ? ImageSourceKey.surHalf : ImageSourceKey.hunHalf);

    /// <summary>
    /// 角色名称全拼
    /// </summary>
    public string FullSpell { get; } = string.Empty;

    /// <summary>
    /// 角色名称简拼
    /// </summary>
    public string Abbrev { get; } = string.Empty;
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name"></param>
    /// <param name="camp"></param>
    /// <param name="imageFileName"></param>
    public Character(string name, Camp camp, string imageFileName)
    {
        Name = name;
        Camp = camp;
        ImageFileName = imageFileName;
        //拼音处理
        var format = PinyinFormat.WITHOUT_TONE | PinyinFormat.LOWERCASE | PinyinFormat.WITH_U_AND_COLON |
                     PinyinFormat.WITH_V;

        var pinyin = Pinyin4Net.GetPinyin(name, format);

        var parts = pinyin.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);

        if (name.StartsWith("调"))
            parts[0] = "tiao";

        //full pinyin without space
        FullSpell = string.Concat(parts);

        //special case
        if (name.Equals("26号守卫"))
        {
            Abbrev = "bb";
        }
        else
        {
            Abbrev = string.Concat(Enumerable.Select<string, char>(parts, p => p[0]));
        }
    }
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="camp">阵营</param>
    public Character(Camp camp)
    {
        Camp = camp;
    }
    
    /// <summary>
    /// 获取图片源
    /// </summary>
    /// <param name="key">图片源键</param>
    /// <returns>图片源</returns>
    private ImageSource? GetImageSource(ImageSourceKey key)
    {
        return ImageHelper.GetImageSourceFromFileName(key, ImageFileName);
    }
}