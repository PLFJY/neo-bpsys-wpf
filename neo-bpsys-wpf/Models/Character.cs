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
    public string FullSpell { get; set; } = string.Empty;
    public string Abbrev { get; set; } = string.Empty;
    public Character(string name, Camp camp, string imageFileName)
    {
        Name = name;
        Camp = camp;
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

    public ImageSource? GetImageSource(ImageSourceKey key)
    {
        return ImageHelper.GetCharacterImageSource(key, Name);
    }
}