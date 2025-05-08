using CommunityToolkit.Mvvm.ComponentModel;
using hyjiacan.py4n;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Helpers;
using System.Windows.Media;

namespace neo_bpsys_wpf.Models;

public class Character
{
    public string Name { get; set; } = string.Empty;
    public Camp Camp { get; }
    public string ImageFileName { get; set; } = string.Empty;
    public ImageSource? BigImage { get; set; }
    public ImageSource? HeaderImage { get; set; }
    public ImageSource? HeaderImage_SingleColor { get; set; }
    public ImageSource? HalfImage { get; set; }
    public string FullSpell { get; set; } = string.Empty;
    public string Abbrev { get; set; } = string.Empty;
    public Character(string name, Camp camp, string imageFileName)
    {
        Name = name;
        Camp = camp;
        ImageFileName = imageFileName;
        BigImage = GetImageSource(camp == Camp.Sur ? ImageSourceKey.surBig : ImageSourceKey.hunBig);
        HeaderImage = GetImageSource(camp == Camp.Sur ? ImageSourceKey.surHeader : ImageSourceKey.hunHeader);
        HeaderImage_SingleColor = GetImageSource(camp == Camp.Sur ? ImageSourceKey.surHeader_singleColor : ImageSourceKey.hunHeader_singleColor);
        HalfImage = GetImageSource(camp == Camp.Sur ? ImageSourceKey.surHalf : ImageSourceKey.hunHalf);
        //Æ´Òô´¦Àí
        var format = PinyinFormat.WITHOUT_TONE | PinyinFormat.LOWERCASE | PinyinFormat.WITH_U_AND_COLON | PinyinFormat.WITH_V;

        var pinyin = Pinyin4Net.GetPinyin(name, format);

        var parts = pinyin.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        if (name[0].Equals('µ÷'))
            parts[0] = "tiao";

        //full pinyin without space
        FullSpell = string.Concat(parts);

        //special case
        if (name.Equals("26ºÅÊØÎÀ"))
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