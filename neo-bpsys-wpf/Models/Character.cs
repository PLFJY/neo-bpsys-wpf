using hyjiacan.py4n;
using neo_bpsys_wpf.Enums;
using System.IO;
using System.Web;
using System.Windows.Media.Imaging;

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

        var pinyin = Pinyin4Net.GetPinyinArray(Name, format);
        
        //full pinyin without space
        FullSpell = string.Concat(pinyin);

        //special case
        if (Name == "26ºÅÊØÎÀ")
        {
            Abbrev = "bb";
        }
        else
        {
            Abbrev = string.Concat(pinyin.Select(p => p[0]));
        }
    }

    public Character(Camp camp)
    {
        Camp = camp;
    }

    public BitmapImage GetImageSource(ImageSourceKey key)
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", key.ToString(), ImageFileName);
        return new BitmapImage(new Uri(path));
    }
}