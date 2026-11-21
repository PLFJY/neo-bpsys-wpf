using hyjiacan.py4n;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Extensions;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;
using System.ComponentModel;
using System.IO;
using System.Text.Json.Serialization;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace neo_bpsys_wpf.Core.Models;

/// <summary>
/// 角色类, 属性设定均由构造函数完成，不存在后续修改
/// </summary>
[FrontedBindingObject]
public class Character : INotifyPropertyChanged
{

    /// <summary>
    /// 角色名称
    /// </summary>
    public string Name { get; } = string.Empty;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 阵营
    /// </summary>
    [FrontedBindingIgnore]
    public Camp Camp { get; }

    /// <summary>
    /// 图片文件名
    /// </summary>
    [FrontedBindingIgnore]
    public string ImageFileName { get; } = string.Empty;

    private ImageSource? _bigImage;
    private int _bigImageLoadStarted;

    /// <summary>
    /// 全身立绘
    /// </summary>
    [JsonIgnore]
    public ImageSource? BigImage
    {
        get
        {
            if (_bigImage is not null)
            {
                return _bigImage;
            }

            BeginLoadPortrait(
                ref _bigImageLoadStarted,
                Camp == Camp.Sur ? ImageSourceKey.surBig : ImageSourceKey.hunBig,
                nameof(BigImage),
                value => _bigImage = value);
            return null;
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
        _headerImageSingleColor ??= HeaderImage?.ToGrayKeepAlpha();

    private ImageSource? _halfImage;
    private int _halfImageLoadStarted;

    /// <summary>
    /// 半身立绘
    /// </summary>
    [JsonIgnore]
    public ImageSource? HalfImage
    {
        get
        {
            if (_halfImage is not null)
            {
                return _halfImage;
            }

            BeginLoadPortrait(
                ref _halfImageLoadStarted,
                Camp == Camp.Sur ? ImageSourceKey.surHalf : ImageSourceKey.hunHalf,
                nameof(HalfImage),
                value => _halfImage = value);
            return null;
        }
    }

    /// <summary>
    /// 角色名称全拼
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWriting)]
    [FrontedBindingIgnore]
    public string FullSpell { get; } = string.Empty;

    /// <summary>
    /// 角色名称简拼
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWriting)]
    [FrontedBindingIgnore]
    public string Abbrev { get; } = string.Empty;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name"></param>
    /// <param name="camp"></param>
    /// <param name="imageFileName"></param>
    /// <param name="abbrev"></param>
    [JsonConstructor]
    public Character(string name, Camp camp, string imageFileName, string? abbrev = null, string? fullSpell = null)
    {
        Name = name;
        Camp = camp;
        ImageFileName = imageFileName;
        //拼音处理
        const PinyinFormat format = PinyinFormat.WITHOUT_TONE | PinyinFormat.LOWERCASE | PinyinFormat.WITH_U_AND_COLON |
                                    PinyinFormat.WITH_V;

        var pinyin = Pinyin4Net.GetPinyin(name, format);

        var parts = pinyin.Split([" "], StringSplitOptions.RemoveEmptyEntries);

        //full pinyin without space
        FullSpell = string.IsNullOrEmpty(fullSpell) ? string.Concat(parts) : fullSpell;

        //Abbreviation
        Abbrev = string.IsNullOrEmpty(abbrev) ? string.Concat(parts.Select(p => p[0])) : abbrev;
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

    /// <summary>
    /// 在后台解码立绘并在完成后通过属性通知回到 UI。
    /// </summary>
    /// <remarks>
    /// 立绘图片远大于头像，首次访问时同步解码会阻塞 UI 线程；这里改为后台解码，
    /// 返回 null 让绑定先保持占位，解码完成后通过 <see cref="PropertyChanged"/> 刷新绑定。
    /// 每个 Character 的每个立绘只启动一次，避免快速切换角色时重复解码同一张图。
    /// </remarks>
    private void BeginLoadPortrait(
        ref int loadStarted,
        ImageSourceKey key,
        string propertyName,
        Action<ImageSource?> apply)
    {
        if (Interlocked.CompareExchange(ref loadStarted, 1, 0) != 0)
        {
            return;
        }

        var fileFullName = string.IsNullOrEmpty(ImageFileName)
            || !ImageHelper.TryResolveResourcePath(key.ToString(), ImageFileName, out var resolvedPortraitPath)
                ? null
                : resolvedPortraitPath;
        if (fileFullName is null || !File.Exists(fileFullName))
        {
            NotifyImageLoaded(propertyName, apply, null);
            return;
        }

        _ = Task.Run(() => LoadPortrait(fileFullName))
            .ContinueWith(
                task => NotifyImageLoaded(
                    propertyName,
                    apply,
                    task.Status == TaskStatus.RanToCompletion ? task.Result : null),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
    }

    private static BitmapSource? LoadPortrait(string fileFullName)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(fileFullName, UriKind.Absolute);
            // OnLoad 确保冻结前已经完成解码，避免把下载中的 BitmapImage 暴露给绑定。
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private void NotifyImageLoaded(string propertyName, Action<ImageSource?> apply, ImageSource? image)
    {
        void Complete()
        {
            apply(image);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess() || dispatcher.HasShutdownStarted)
        {
            Complete();
            return;
        }

        dispatcher.BeginInvoke(Complete);
    }
}
