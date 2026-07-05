using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace neo_bpsys_wpf.TeamJsonMaker;

/// <summary>
/// 队伍 JSON 导出模型。
/// </summary>
public partial class Team : ObservableObjectBase
{
    /// <summary>
    /// 初始化 <see cref="Team"/> 类的新实例，并创建默认队员槽位。
    /// </summary>
    public Team()
    {
        for (var i = 0; i < 4; i++)
        {
            SurMemberList.Add(new Member(Camp.Sur));
        }

        HunMemberList.Add(new Member(Camp.Hun));
    }

    /// <summary>
    /// 获取或设置队伍名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    private string _colorHex = "#FF337FB9";

    /// <summary>
    /// 获取或设置队伍颜色的十六进制文本，格式为 #AARRGGBB。
    /// </summary>
    public string ColorHex
    {
        get => _colorHex;
        set
        {
            if (ColorHelper.TryNormalizeHex(value, out var normalized))
            {
                if (SetProperty(ref _colorHex, normalized))
                {
                    OnPropertyChanged(nameof(Color));
                }
            }
        }
    }

    /// <summary>
    /// 获取或设置供颜色选择器使用的队伍颜色。
    /// </summary>
    [JsonIgnore]
    public Color Color
    {
        get => ColorHelper.ParseColorOrDefault(ColorHex, Colors.White);
        set => ColorHex = value.ToArgbHexString();
    }

    /// <summary>
    /// 求生者队员列表
    /// </summary>
    public ObservableCollection<Member> SurMemberList { get; } = [];

    /// <summary>
    /// 监管者队员列表
    /// </summary>
    public ObservableCollection<Member> HunMemberList { get; } = [];

    /// <summary>
    /// 获取或设置队伍 LOGO 的图片 URI。
    /// </summary>
    public string ImageUri { get; set; } = string.Empty;
}
