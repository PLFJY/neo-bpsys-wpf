using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Helpers;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace neo_bpsys_wpf.ViewModels.Windows;

/// <summary>
/// 为单个生成的动画部件提供经过校验的编辑缓冲区。
/// </summary>
public sealed class FrontedAnimationPartEditorViewModel : ObservableValidator
{
    private readonly Func<string, bool> _isNameAvailable;
    private string _name = string.Empty;
    private FrontedAnimationPartKind _kind;
    private FrontedAnimationPartLayer _layer;
    private string _widthText = string.Empty;
    private string _heightText = string.Empty;
    private string _leftText = "0";
    private string _topText = "0";
    private string _fill = string.Empty;
    private Color _fillColor = FrontedPropertyColorHelper.FallbackColor;
    private string _stroke = string.Empty;
    private Color _strokeColor = FrontedPropertyColorHelper.FallbackColor;
    private string _strokeThicknessText = "0";
    private string _imagePath = string.Empty;
    private string _opacityText = "1";
    private string _visibility = "Hidden";
    private string _zIndexText = "0";
    private bool _isHitTestVisible;
    private FrontedVisualEffectKind _effectKind;
    private string _effectColor = string.Empty;
    private Color _effectPickerColor = FrontedPropertyColorHelper.FallbackColor;
    private string _effectOpacityText = "1";
    private string _effectBlurRadiusText = "0";
    private string _effectShadowDepthText = "0";
    private string _effectDirectionText = "0";
    private bool _isSynchronizingColor;

    /// <summary>
    /// 初始化经过校验的动画部件编辑器。
    /// </summary>
    /// <param name="source">源动画部件配置。</param>
    /// <param name="isNameAvailable">用于检查编辑后的名称在其父控件内是否唯一的回调。</param>
    public FrontedAnimationPartEditorViewModel(
        FrontedAnimationPartConfig source,
        Func<string, bool> isNameAvailable)
    {
        _isNameAvailable = isNameAvailable;
        Load(source);
        ValidateAll();
    }

    /// <summary>
    /// 获取或设置用户定义的动画部件名称。
    /// </summary>
    [CustomValidation(typeof(FrontedAnimationPartEditorViewModel), nameof(ValidateNameValue))]
    public string Name
    {
        get => _name;
        set => SetValidatedProperty(ref _name, value ?? string.Empty);
    }

    /// <summary>
    /// 获取或设置生成的元素类型。
    /// </summary>
    public FrontedAnimationPartKind Kind
    {
        get => _kind;
        set
        {
            if (!SetProperty(ref _kind, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsRectangle));
            OnPropertyChanged(nameof(IsBorder));
            OnPropertyChanged(nameof(IsImage));
            OnPropertyChanged(nameof(IsShape));
        }
    }

    /// <summary>
    /// 获取一个值，指示正在编辑的部件是否为矩形。
    /// </summary>
    public bool IsRectangle => Kind == FrontedAnimationPartKind.Rectangle;

    /// <summary>
    /// 获取一个值，指示正在编辑的部件是否为边框。
    /// </summary>
    public bool IsBorder => Kind == FrontedAnimationPartKind.Border;

    /// <summary>
    /// 获取一个值，指示正在编辑的部件是否为图片。
    /// </summary>
    public bool IsImage => Kind == FrontedAnimationPartKind.Image;

    /// <summary>
    /// 获取一个值，指示正在编辑的部件是否支持填充和描边画刷。
    /// </summary>
    public bool IsShape => Kind is FrontedAnimationPartKind.Rectangle or FrontedAnimationPartKind.Border;

    /// <summary>
    /// 获取或设置生成的元素层。
    /// </summary>
    public FrontedAnimationPartLayer Layer
    {
        get => _layer;
        set => SetProperty(ref _layer, value);
    }

    /// <summary>
    /// 获取或设置宽度表达式。
    /// </summary>
    [CustomValidation(typeof(FrontedAnimationPartEditorViewModel), nameof(ValidateSizeValue))]
    public string WidthText
    {
        get => _widthText;
        set => SetValidatedProperty(ref _widthText, value ?? string.Empty);
    }

    /// <summary>
    /// 获取或设置高度表达式。
    /// </summary>
    [CustomValidation(typeof(FrontedAnimationPartEditorViewModel), nameof(ValidateSizeValue))]
    public string HeightText
    {
        get => _heightText;
        set => SetValidatedProperty(ref _heightText, value ?? string.Empty);
    }

    /// <summary>
    /// 获取或设置左侧偏移文本。
    /// </summary>
    [CustomValidation(typeof(FrontedAnimationPartEditorViewModel), nameof(ValidateNumberValue))]
    public string LeftText
    {
        get => _leftText;
        set => SetValidatedProperty(ref _leftText, value ?? string.Empty);
    }

    /// <summary>
    /// 获取或设置顶部偏移文本。
    /// </summary>
    [CustomValidation(typeof(FrontedAnimationPartEditorViewModel), nameof(ValidateNumberValue))]
    public string TopText
    {
        get => _topText;
        set => SetValidatedProperty(ref _topText, value ?? string.Empty);
    }

    /// <summary>
    /// 获取或设置填充颜色文本。
    /// </summary>
    [CustomValidation(typeof(FrontedAnimationPartEditorViewModel), nameof(ValidateColorValue))]
    public string Fill
    {
        get => _fill;
        set
        {
            if (!SetValidatedProperty(ref _fill, value ?? string.Empty)
                || _isSynchronizingColor
                || !FrontedPropertyColorHelper.TryParseArgbColor(_fill, out var color))
            {
                return;
            }

            SetProperty(ref _fillColor, color, nameof(FillColor));
        }
    }

    /// <summary>
    /// 获取或设置填充颜色的取色器值。
    /// </summary>
    public Color FillColor
    {
        get => _fillColor;
        set => SetColorFromPicker(ref _fillColor, value, nameof(FillColor), colorText => Fill = colorText);
    }

    /// <summary>
    /// 获取或设置描边颜色文本。
    /// </summary>
    [CustomValidation(typeof(FrontedAnimationPartEditorViewModel), nameof(ValidateColorValue))]
    public string Stroke
    {
        get => _stroke;
        set
        {
            if (!SetValidatedProperty(ref _stroke, value ?? string.Empty)
                || _isSynchronizingColor
                || !FrontedPropertyColorHelper.TryParseArgbColor(_stroke, out var color))
            {
                return;
            }

            SetProperty(ref _strokeColor, color, nameof(StrokeColor));
        }
    }

    /// <summary>
    /// 获取或设置描边颜色的取色器值。
    /// </summary>
    public Color StrokeColor
    {
        get => _strokeColor;
        set => SetColorFromPicker(ref _strokeColor, value, nameof(StrokeColor), colorText => Stroke = colorText);
    }

    /// <summary>
    /// 获取或设置描边粗细文本。
    /// </summary>
    [CustomValidation(typeof(FrontedAnimationPartEditorViewModel), nameof(ValidateNonNegativeNumberValue))]
    public string StrokeThicknessText
    {
        get => _strokeThicknessText;
        set => SetValidatedProperty(ref _strokeThicknessText, value ?? string.Empty);
    }

    /// <summary>
    /// 获取或设置图片资源路径。
    /// </summary>
    [CustomValidation(typeof(FrontedAnimationPartEditorViewModel), nameof(ValidateResourcePathValue))]
    public string ImagePath
    {
        get => _imagePath;
        set => SetValidatedProperty(ref _imagePath, value ?? string.Empty);
    }

    /// <summary>
    /// 获取或设置不透明度文本。
    /// </summary>
    [CustomValidation(typeof(FrontedAnimationPartEditorViewModel), nameof(ValidateOpacityValue))]
    public string OpacityText
    {
        get => _opacityText;
        set => SetValidatedProperty(ref _opacityText, value ?? string.Empty);
    }

    /// <summary>
    /// 获取或设置初始 WPF 可见性名称。
    /// </summary>
    public string Visibility
    {
        get => _visibility;
        set => SetProperty(ref _visibility, value);
    }

    /// <summary>
    /// 获取或设置层内 z-index 文本。
    /// </summary>
    [CustomValidation(typeof(FrontedAnimationPartEditorViewModel), nameof(ValidateIntegerValue))]
    public string ZIndexText
    {
        get => _zIndexText;
        set => SetValidatedProperty(ref _zIndexText, value ?? string.Empty);
    }

    /// <summary>
    /// 获取或设置生成的部件是否参与命中测试。
    /// </summary>
    public bool IsHitTestVisible
    {
        get => _isHitTestVisible;
        set => SetProperty(ref _isHitTestVisible, value);
    }

    /// <summary>
    /// 获取或设置视觉效果类型。
    /// </summary>
    public FrontedVisualEffectKind EffectKind
    {
        get => _effectKind;
        set
        {
            if (SetProperty(ref _effectKind, value))
            {
                OnPropertyChanged(nameof(IsShadowEffect));
            }
        }
    }

    /// <summary>获取一个值，指示当前效果是否显示投影/发光参数。</summary>
    public bool IsShadowEffect => EffectKind is FrontedVisualEffectKind.Glow or FrontedVisualEffectKind.DropShadow;

    /// <summary>
    /// 获取或设置视觉效果颜色文本。
    /// </summary>
    [CustomValidation(typeof(FrontedAnimationPartEditorViewModel), nameof(ValidateColorValue))]
    public string EffectColor
    {
        get => _effectColor;
        set
        {
            if (!SetValidatedProperty(ref _effectColor, value ?? string.Empty)
                || _isSynchronizingColor
                || !FrontedPropertyColorHelper.TryParseArgbColor(_effectColor, out var color))
            {
                return;
            }

            SetProperty(ref _effectPickerColor, color, nameof(EffectPickerColor));
        }
    }

    /// <summary>
    /// 获取或设置视觉效果的取色器值。
    /// </summary>
    public Color EffectPickerColor
    {
        get => _effectPickerColor;
        set => SetColorFromPicker(ref _effectPickerColor, value, nameof(EffectPickerColor), colorText => EffectColor = colorText);
    }

    /// <summary>
    /// 获取或设置视觉效果不透明度文本。
    /// </summary>
    [CustomValidation(typeof(FrontedAnimationPartEditorViewModel), nameof(ValidateOpacityValue))]
    public string EffectOpacityText
    {
        get => _effectOpacityText;
        set => SetValidatedProperty(ref _effectOpacityText, value ?? string.Empty);
    }

    /// <summary>
    /// 获取或设置视觉效果模糊半径文本。
    /// </summary>
    [CustomValidation(typeof(FrontedAnimationPartEditorViewModel), nameof(ValidateNonNegativeNumberValue))]
    public string EffectBlurRadiusText
    {
        get => _effectBlurRadiusText;
        set => SetValidatedProperty(ref _effectBlurRadiusText, value ?? string.Empty);
    }

    /// <summary>
    /// 获取或设置视觉效果阴影深度文本。
    /// </summary>
    [CustomValidation(typeof(FrontedAnimationPartEditorViewModel), nameof(ValidateNonNegativeNumberValue))]
    public string EffectShadowDepthText
    {
        get => _effectShadowDepthText;
        set => SetValidatedProperty(ref _effectShadowDepthText, value ?? string.Empty);
    }

    /// <summary>
    /// 获取或设置视觉效果方向文本。
    /// </summary>
    [CustomValidation(typeof(FrontedAnimationPartEditorViewModel), nameof(ValidateNumberValue))]
    public string EffectDirectionText
    {
        get => _effectDirectionText;
        set => SetValidatedProperty(ref _effectDirectionText, value ?? string.Empty);
    }

    /// <summary>
    /// 校验所有可编辑文本字段。
    /// </summary>
    public void ValidateAll()
    {
        ValidateAllProperties();
    }

    /// <summary>
    /// 将经过校验的编辑器值复制到动画部件配置。
    /// </summary>
    /// <param name="target">目标动画部件配置。</param>
    /// <exception cref="InvalidOperationException">编辑器存在校验错误时抛出。</exception>
    public void ApplyTo(FrontedAnimationPartConfig target)
    {
        ValidateAll();
        if (HasErrors)
        {
            throw new InvalidOperationException("Cannot apply an invalid animation part editor.");
        }

        target.Name = Name.Trim();
        target.Kind = Kind;
        target.Layer = Layer;
        ApplySize(WidthText, out var width, out var widthText);
        ApplySize(HeightText, out var height, out var heightText);
        target.Width = width;
        target.Height = height;
        target.WidthText = widthText;
        target.HeightText = heightText;
        target.Left = ParseDouble(LeftText);
        target.Top = ParseDouble(TopText);
        target.Fill = NullIfWhiteSpace(Fill);
        target.Stroke = NullIfWhiteSpace(Stroke);
        target.StrokeThickness = ParseDouble(StrokeThicknessText);
        target.ImagePath = NullIfWhiteSpace(ImagePath);
        target.Opacity = ParseDouble(OpacityText);
        target.Visibility = Visibility;
        target.ZIndex = int.Parse(ZIndexText, NumberStyles.Integer, CultureInfo.InvariantCulture);
        target.IsHitTestVisible = IsHitTestVisible;
        target.Effect = new FrontedVisualEffectConfig
        {
            Kind = EffectKind,
            Color = NullIfWhiteSpace(EffectColor),
            Opacity = ParseDouble(EffectOpacityText),
            BlurRadius = ParseDouble(EffectBlurRadiusText),
            ShadowDepth = ParseDouble(EffectShadowDepthText),
            Direction = ParseDouble(EffectDirectionText)
        };
    }

    private void Load(FrontedAnimationPartConfig source)
    {
        _name = source.Name;
        _kind = source.Kind;
        _layer = source.Layer;
        _widthText = FormatSize(source.Width, source.WidthText);
        _heightText = FormatSize(source.Height, source.HeightText);
        _leftText = source.Left.ToString(CultureInfo.InvariantCulture);
        _topText = source.Top.ToString(CultureInfo.InvariantCulture);
        _fill = source.Fill ?? string.Empty;
        _fillColor = FrontedPropertyColorHelper.TryParseArgbColor(_fill, out var fillColor)
            ? fillColor
            : FrontedPropertyColorHelper.FallbackColor;
        _stroke = source.Stroke ?? string.Empty;
        _strokeColor = FrontedPropertyColorHelper.TryParseArgbColor(_stroke, out var strokeColor)
            ? strokeColor
            : FrontedPropertyColorHelper.FallbackColor;
        _strokeThicknessText = source.StrokeThickness.ToString(CultureInfo.InvariantCulture);
        _imagePath = source.ImagePath ?? string.Empty;
        _opacityText = source.Opacity.ToString(CultureInfo.InvariantCulture);
        _visibility = source.Visibility;
        _zIndexText = source.ZIndex.ToString(CultureInfo.InvariantCulture);
        _isHitTestVisible = source.IsHitTestVisible;
        _effectKind = source.Effect.Kind;
        _effectColor = source.Effect.Color ?? string.Empty;
        _effectPickerColor = FrontedPropertyColorHelper.TryParseArgbColor(_effectColor, out var effectColor)
            ? effectColor
            : FrontedPropertyColorHelper.FallbackColor;
        _effectOpacityText = source.Effect.Opacity.ToString(CultureInfo.InvariantCulture);
        _effectBlurRadiusText = source.Effect.BlurRadius.ToString(CultureInfo.InvariantCulture);
        _effectShadowDepthText = source.Effect.ShadowDepth.ToString(CultureInfo.InvariantCulture);
        _effectDirectionText = source.Effect.Direction.ToString(CultureInfo.InvariantCulture);
    }

    private bool SetValidatedProperty(
        ref string storage,
        string value,
        [CallerMemberName] string propertyName = "")
    {
        return SetProperty(ref storage, value, true, propertyName);
    }

    /// <summary>
    /// 校验动画部件名称。
    /// </summary>
    /// <param name="value">候选名称。</param>
    /// <param name="context">校验上下文。</param>
    /// <returns>校验结果。</returns>
    public static ValidationResult? ValidateNameValue(string value, ValidationContext context)
    {
        var editor = (FrontedAnimationPartEditorViewModel)context.ObjectInstance;
        return string.IsNullOrWhiteSpace(value) || !editor._isNameAvailable(value.Trim())
            ? new ValidationResult(I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.AnimationParts.InvalidName"))
            : ValidationResult.Success;
    }

    /// <summary>
    /// 校验像素或百分比尺寸表达式。
    /// </summary>
    /// <param name="value">候选表达式。</param>
    /// <param name="context">校验上下文。</param>
    /// <returns>校验结果。</returns>
    public static ValidationResult? ValidateSizeValue(string value, ValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ValidationResult.Success;
        }

        var numericText = value.Trim();
        if (numericText.EndsWith('%'))
        {
            numericText = numericText[..^1];
        }

        return TryParseFiniteDouble(numericText, out var number) && number >= 0D
            ? ValidationResult.Success
            : new ValidationResult(I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.AnimationParts.Validation.Size"));
    }

    /// <summary>
    /// 校验有限数值。
    /// </summary>
    /// <param name="value">候选数值。</param>
    /// <param name="context">校验上下文。</param>
    /// <returns>校验结果。</returns>
    public static ValidationResult? ValidateNumberValue(string value, ValidationContext context) =>
        TryParseFiniteDouble(value, out _)
            ? ValidationResult.Success
            : new ValidationResult(I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.AnimationParts.Validation.Number"));

    /// <summary>
    /// 校验非负有限数值。
    /// </summary>
    /// <param name="value">候选数值。</param>
    /// <param name="context">校验上下文。</param>
    /// <returns>校验结果。</returns>
    public static ValidationResult? ValidateNonNegativeNumberValue(string value, ValidationContext context) =>
        TryParseFiniteDouble(value, out var number) && number >= 0D
            ? ValidationResult.Success
            : new ValidationResult(I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.AnimationParts.Validation.NonNegativeNumber"));

    /// <summary>
    /// 校验不透明度值。
    /// </summary>
    /// <param name="value">候选不透明度。</param>
    /// <param name="context">校验上下文。</param>
    /// <returns>校验结果。</returns>
    public static ValidationResult? ValidateOpacityValue(string value, ValidationContext context) =>
        TryParseFiniteDouble(value, out var number) && number is >= 0D and <= 1D
            ? ValidationResult.Success
            : new ValidationResult(I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.AnimationParts.Validation.Opacity"));

    /// <summary>
    /// 校验整数值。
    /// </summary>
    /// <param name="value">候选整数。</param>
    /// <param name="context">校验上下文。</param>
    /// <returns>校验结果。</returns>
    public static ValidationResult? ValidateIntegerValue(string value, ValidationContext context) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            ? ValidationResult.Success
            : new ValidationResult(I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.AnimationParts.Validation.Integer"));

    /// <summary>
    /// 校验可选的 WPF 颜色。
    /// </summary>
    /// <param name="value">候选颜色。</param>
    /// <param name="context">校验上下文。</param>
    /// <returns>校验结果。</returns>
    public static ValidationResult? ValidateColorValue(string value, ValidationContext context) =>
        string.IsNullOrWhiteSpace(value) || FrontedPropertyColorHelper.TryParseArgbColor(value, out _)
            ? ValidationResult.Success
            : new ValidationResult(I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.AnimationParts.Validation.Color"));

    /// <summary>
    /// 校验布局包资源路径。
    /// </summary>
    /// <param name="value">候选资源路径。</param>
    /// <param name="context">校验上下文。</param>
    /// <returns>校验结果。</returns>
    public static ValidationResult? ValidateResourcePathValue(string value, ValidationContext context) =>
        value.Length <= FrontedLayoutLimits.MaxResourcePathLength
            ? ValidationResult.Success
            : new ValidationResult(I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "ResourcePathTooLong"));

    private void SetColorFromPicker(
        ref Color storage,
        Color value,
        string propertyName,
        Action<string> setText)
    {
        if (!SetProperty(ref storage, value, propertyName))
        {
            return;
        }

        _isSynchronizingColor = true;
        try
        {
            setText(FrontedPropertyColorHelper.ToArgbString(value));
        }
        finally
        {
            _isSynchronizingColor = false;
        }
    }

    private static bool TryParseFiniteDouble(string value, out double number) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number)
        && double.IsFinite(number);

    private static double ParseDouble(string value) =>
        double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);

    private static string FormatSize(double? fixedValue, string? textValue) =>
        !string.IsNullOrWhiteSpace(textValue)
            ? textValue
            : fixedValue?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    private static void ApplySize(string value, out double? fixedValue, out string? textValue)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            fixedValue = null;
            textValue = null;
        }
        else if (trimmed.EndsWith('%'))
        {
            fixedValue = null;
            textValue = trimmed;
        }
        else
        {
            fixedValue = ParseDouble(trimmed);
            textValue = null;
        }
    }

    private static string? NullIfWhiteSpace(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
