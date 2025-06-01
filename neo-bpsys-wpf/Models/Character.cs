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
    public string Name { get;  } = string.Empty;
    public Camp Camp { get; }
    public string ImageFileName { get; } = string.Empty;
    [JsonIgnore]
    public ImageSource? BigImage { get;  }
    [JsonIgnore]
    public ImageSource? HeaderImage { get; }
    [JsonIgnore]
    public ImageSource? HeaderImage_SingleColor { get;  }
    [JsonIgnore]
    public ImageSource? HalfImage { get;  }
    public string FullSpell { get;  } = string.Empty;// 角色名称全拼
    public string Abbrev { get;  } = string.Empty; //角色名称简拼
    public Character(string name, Camp camp, string imageFileName)
    {
        Name = name;
        Camp = camp;
        ImageFileName = imageFileName;
        BigImage = GetImageSource(camp == Camp.Sur ? ImageSourceKey.surBig : ImageSourceKey.hunBig);
        HeaderImage = GetImageSource(camp == Camp.Sur ? ImageSourceKey.surHeader : ImageSourceKey.hunHeader);
        HeaderImage_SingleColor = GetImageSource(camp == Camp.Sur ? ImageSourceKey.surHeader_singleColor : ImageSourceKey.hunHeader_singleColor);
        HalfImage = GetImageSource(camp == Camp.Sur ? ImageSourceKey.surHalf : ImageSourceKey.hunHalf);
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