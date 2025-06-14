using hyjiacan.py4n;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Helpers;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace neo_bpsys_wpf.Models;

/// <summary>
/// 角色类, 属性设定均由构造函数完成，不存在后续修改
/// </summary>
public class Character
{
    private ImageSource? _bigImage;
    private ImageSource? _headerImage;
    private ImageSource? _headerImageSingleColor;
    private ImageSource? _halfImage;
    public string Name { get;  } = string.Empty;
    public Camp Camp { get; }
    public string ImageFileName { get; } = string.Empty;

    [JsonIgnore]
    public ImageSource? BigImage
    {
        get
        {
            if (_bigImage == null)
            {
                _bigImage = GetImageSource(Camp == Camp.Sur ? ImageSourceKey.surBig : ImageSourceKey.hunBig);
            }
            return _bigImage ;
        }
    }

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

    [JsonIgnore]
    public ImageSource? HeaderImageSingleColor
    {
        get
        {
            if (_headerImageSingleColor == null)
            {
                _headerImageSingleColor = GetImageSource(Camp == Camp.Sur ? ImageSourceKey.surHeader_singleColor : ImageSourceKey.hunHeader_singleColor);
            }
            return _headerImageSingleColor;
        }
    }

    [JsonIgnore]
    public ImageSource? HalfImage
    {
        get
        {
            if (_halfImage == null)
            {
                _halfImage = GetImageSource(Camp == Camp.Sur ? ImageSourceKey.surHalf : ImageSourceKey.hunHalf);
            }
            return _halfImage;
        }
    }

    public string FullSpell { get;  } = string.Empty;// 角色名称全拼
    public string Abbrev { get;  } = string.Empty; //角色名称简拼
    public Character(string name, Camp camp, string imageFileName)
    {
        Name = name;
        Camp = camp;
        ImageFileName = imageFileName;
        //拼音处理
        var format = PinyinFormat.WITHOUT_TONE | PinyinFormat.LOWERCASE | PinyinFormat.WITH_U_AND_COLON | PinyinFormat.WITH_V;

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
            Abbrev = string.Concat(parts.Select(p => p[0]));
        }
    }
    public Character(Camp camp)
    {
        Camp = camp;
    }

    private ImageSource? GetImageSource(ImageSourceKey key)
    {
        return ImageHelper.GetImageSourceFromFileName(key, ImageFileName);
    }
}