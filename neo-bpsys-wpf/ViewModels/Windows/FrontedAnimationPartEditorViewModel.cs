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
/// Provides validated edit buffers for one generated animation part.
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
    /// Initializes a validated animation part editor.
    /// </summary>
    /// <param name="source">Source animation part configuration.</param>
    /// <param name="isNameAvailable">Checks whether an edited name is unique within its parent control.</param>
    public FrontedAnimationPartEditorViewModel(
        FrontedAnimationPartConfig source,
        Func<string, bool> isNameAvailable)
    {
        _isNameAvailable = isNameAvailable;
        Load(source);
        ValidateAll();
    }

    /// <summary>
    /// Gets or sets the user-defined animation part name.
    /// </summary>
    [CustomValidation(typeof(FrontedAnimationPartEditorViewModel), nameof(ValidateNameValue))]
    public string Name
    {
        get => _name;
        set => SetValidatedProperty(ref _name, value ?? string.Empty);
    }

    /// <summary>
    /// Gets or sets the generated element kind.
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
    /// Gets a value indicating whether the edited part is a rectangle.
    /// </summary>
    public bool IsRectangle => Kind == FrontedAnimationPartKind.Rectangle;

    /// <summary>
    /// Gets a value indicating whether the edited part is a border.
    /// </summary>
    public bool IsBorder => Kind == FrontedAnimationPartKind.Border;

    /// <summary>
    /// Gets a value indicating whether the edited part is an image.
    /// </summary>
    public bool IsImage => Kind == FrontedAnimationPartKind.Image;

    /// <summary>
    /// Gets a value indicating whether the edited part supports fill and stroke brushes.
    /// </summary>
    public bool IsShape => Kind is FrontedAnimationPartKind.Rectangle or FrontedAnimationPartKind.Border;

    /// <summary>
    /// Gets or sets the generated element layer.
    /// </summary>
    public FrontedAnimationPartLayer Layer
    {
        get => _layer;
        set => SetProperty(ref _layer, value);
    }

    /// <summary>
    /// Gets or sets the width expression.
    /// </summary>
    [CustomValidation(typeof(FrontedAnimationPartEditorViewModel), nameof(ValidateSizeValue))]
    public string WidthText
    {
        get => _widthText;
        set => SetValidatedProperty(ref _widthText, value ?? string.Empty);
    }

    /// <summary>
    /// Gets or sets the height expression.
    /// </summary>
    [CustomValidation(typeof(FrontedAnimationPartEditorViewModel), nameof(ValidateSizeValue))]
    public string HeightText
    {
        get => _heightText;
        set => SetValidatedProperty(ref _heightText, value ?? string.Empty);
    }

    /// <summary>
    /// Gets or sets the left offset text.
    /// </summary>
    [CustomValidation(typeof(FrontedAnimationPartEditorViewModel), nameof(ValidateNumberValue))]
    public string LeftText
    {
        get => _leftText;
        set => SetValidatedProperty(ref _leftText, value ?? string.Empty);
    }

    /// <summary>
    /// Gets or sets the top offset text.
    /// </summary>
    [CustomValidation(typeof(FrontedAnimationPartEditorViewModel), nameof(ValidateNumberValue))]
    public string TopText
    {
        get => _topText;
        set => SetValidatedProperty(ref _topText, value ?? string.Empty);
    }

    /// <summary>
    /// Gets or sets the fill color text.
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
    /// Gets or sets the color-picker value for the fill.
    /// </summary>
    public Color FillColor
    {
        get => _fillColor;
        set => SetColorFromPicker(ref _fillColor, value, nameof(FillColor), colorText => Fill = colorText);
    }

    /// <summary>
    /// Gets or sets the stroke color text.
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
    /// Gets or sets the color-picker value for the stroke.
    /// </summary>
    public Color StrokeColor
    {
        get => _strokeColor;
        set => SetColorFromPicker(ref _strokeColor, value, nameof(StrokeColor), colorText => Stroke = colorText);
    }

    /// <summary>
    /// Gets or sets the stroke thickness text.
    /// </summary>
    [CustomValidation(typeof(FrontedAnimationPartEditorViewModel), nameof(ValidateNonNegativeNumberValue))]
    public string StrokeThicknessText
    {
        get => _strokeThicknessText;
        set => SetValidatedProperty(ref _strokeThicknessText, value ?? string.Empty);
    }

    /// <summary>
    /// Gets or sets the image resource path.
    /// </summary>
    [CustomValidation(typeof(FrontedAnimationPartEditorViewModel), nameof(ValidateResourcePathValue))]
    public string ImagePath
    {
        get => _imagePath;
        set => SetValidatedProperty(ref _imagePath, value ?? string.Empty);
    }

    /// <summary>
    /// Gets or sets the opacity text.
    /// </summary>
    [CustomValidation(typeof(FrontedAnimationPartEditorViewModel), nameof(ValidateOpacityValue))]
    public string OpacityText
    {
        get => _opacityText;
        set => SetValidatedProperty(ref _opacityText, value ?? string.Empty);
    }

    /// <summary>
    /// Gets or sets the initial WPF visibility name.
    /// </summary>
    public string Visibility
    {
        get => _visibility;
        set => SetProperty(ref _visibility, value);
    }

    /// <summary>
    /// Gets or sets the layer-local z-index text.
    /// </summary>
    [CustomValidation(typeof(FrontedAnimationPartEditorViewModel), nameof(ValidateIntegerValue))]
    public string ZIndexText
    {
        get => _zIndexText;
        set => SetValidatedProperty(ref _zIndexText, value ?? string.Empty);
    }

    /// <summary>
    /// Gets or sets whether the generated part participates in hit testing.
    /// </summary>
    public bool IsHitTestVisible
    {
        get => _isHitTestVisible;
        set => SetProperty(ref _isHitTestVisible, value);
    }

    /// <summary>
    /// Gets or sets the visual effect kind.
    /// </summary>
    public FrontedVisualEffectKind EffectKind
    {
        get => _effectKind;
        set => SetProperty(ref _effectKind, value);
    }

    /// <summary>
    /// Gets or sets the visual effect color text.
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
    /// Gets or sets the color-picker value for the visual effect.
    /// </summary>
    public Color EffectPickerColor
    {
        get => _effectPickerColor;
        set => SetColorFromPicker(ref _effectPickerColor, value, nameof(EffectPickerColor), colorText => EffectColor = colorText);
    }

    /// <summary>
    /// Gets or sets the visual effect opacity text.
    /// </summary>
    [CustomValidation(typeof(FrontedAnimationPartEditorViewModel), nameof(ValidateOpacityValue))]
    public string EffectOpacityText
    {
        get => _effectOpacityText;
        set => SetValidatedProperty(ref _effectOpacityText, value ?? string.Empty);
    }

    /// <summary>
    /// Gets or sets the visual effect blur radius text.
    /// </summary>
    [CustomValidation(typeof(FrontedAnimationPartEditorViewModel), nameof(ValidateNonNegativeNumberValue))]
    public string EffectBlurRadiusText
    {
        get => _effectBlurRadiusText;
        set => SetValidatedProperty(ref _effectBlurRadiusText, value ?? string.Empty);
    }

    /// <summary>
    /// Gets or sets the visual effect shadow depth text.
    /// </summary>
    [CustomValidation(typeof(FrontedAnimationPartEditorViewModel), nameof(ValidateNonNegativeNumberValue))]
    public string EffectShadowDepthText
    {
        get => _effectShadowDepthText;
        set => SetValidatedProperty(ref _effectShadowDepthText, value ?? string.Empty);
    }

    /// <summary>
    /// Gets or sets the visual effect direction text.
    /// </summary>
    [CustomValidation(typeof(FrontedAnimationPartEditorViewModel), nameof(ValidateNumberValue))]
    public string EffectDirectionText
    {
        get => _effectDirectionText;
        set => SetValidatedProperty(ref _effectDirectionText, value ?? string.Empty);
    }

    /// <summary>
    /// Validates every editable text field.
    /// </summary>
    public void ValidateAll()
    {
        ValidateAllProperties();
    }

    /// <summary>
    /// Copies the validated editor values to a animation part configuration.
    /// </summary>
    /// <param name="target">Target animation part configuration.</param>
    /// <exception cref="InvalidOperationException">Thrown when the editor contains validation errors.</exception>
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
    /// Validates a animation part name.
    /// </summary>
    /// <param name="value">Candidate name.</param>
    /// <param name="context">Validation context.</param>
    /// <returns>The validation result.</returns>
    public static ValidationResult? ValidateNameValue(string value, ValidationContext context)
    {
        var editor = (FrontedAnimationPartEditorViewModel)context.ObjectInstance;
        return string.IsNullOrWhiteSpace(value) || !editor._isNameAvailable(value.Trim())
            ? new ValidationResult(I18nHelper.GetLocalizedString(AppI18nDictionaries.AnimationEditor, "Designer.AnimationParts.InvalidName"))
            : ValidationResult.Success;
    }

    /// <summary>
    /// Validates a pixel or percentage size expression.
    /// </summary>
    /// <param name="value">Candidate expression.</param>
    /// <param name="context">Validation context.</param>
    /// <returns>The validation result.</returns>
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
            : new ValidationResult(I18nHelper.GetLocalizedString(AppI18nDictionaries.AnimationEditor, "Designer.AnimationParts.Validation.Size"));
    }

    /// <summary>
    /// Validates a finite number.
    /// </summary>
    /// <param name="value">Candidate number.</param>
    /// <param name="context">Validation context.</param>
    /// <returns>The validation result.</returns>
    public static ValidationResult? ValidateNumberValue(string value, ValidationContext context) =>
        TryParseFiniteDouble(value, out _)
            ? ValidationResult.Success
            : new ValidationResult(I18nHelper.GetLocalizedString(AppI18nDictionaries.AnimationEditor, "Designer.AnimationParts.Validation.Number"));

    /// <summary>
    /// Validates a non-negative finite number.
    /// </summary>
    /// <param name="value">Candidate number.</param>
    /// <param name="context">Validation context.</param>
    /// <returns>The validation result.</returns>
    public static ValidationResult? ValidateNonNegativeNumberValue(string value, ValidationContext context) =>
        TryParseFiniteDouble(value, out var number) && number >= 0D
            ? ValidationResult.Success
            : new ValidationResult(I18nHelper.GetLocalizedString(AppI18nDictionaries.AnimationEditor, "Designer.AnimationParts.Validation.NonNegativeNumber"));

    /// <summary>
    /// Validates an opacity value.
    /// </summary>
    /// <param name="value">Candidate opacity.</param>
    /// <param name="context">Validation context.</param>
    /// <returns>The validation result.</returns>
    public static ValidationResult? ValidateOpacityValue(string value, ValidationContext context) =>
        TryParseFiniteDouble(value, out var number) && number is >= 0D and <= 1D
            ? ValidationResult.Success
            : new ValidationResult(I18nHelper.GetLocalizedString(AppI18nDictionaries.AnimationEditor, "Designer.AnimationParts.Validation.Opacity"));

    /// <summary>
    /// Validates an integer value.
    /// </summary>
    /// <param name="value">Candidate integer.</param>
    /// <param name="context">Validation context.</param>
    /// <returns>The validation result.</returns>
    public static ValidationResult? ValidateIntegerValue(string value, ValidationContext context) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            ? ValidationResult.Success
            : new ValidationResult(I18nHelper.GetLocalizedString(AppI18nDictionaries.AnimationEditor, "Designer.AnimationParts.Validation.Integer"));

    /// <summary>
    /// Validates an optional WPF color.
    /// </summary>
    /// <param name="value">Candidate color.</param>
    /// <param name="context">Validation context.</param>
    /// <returns>The validation result.</returns>
    public static ValidationResult? ValidateColorValue(string value, ValidationContext context) =>
        string.IsNullOrWhiteSpace(value) || FrontedPropertyColorHelper.TryParseArgbColor(value, out _)
            ? ValidationResult.Success
            : new ValidationResult(I18nHelper.GetLocalizedString(AppI18nDictionaries.AnimationEditor, "Designer.AnimationParts.Validation.Color"));

    /// <summary>
    /// Validates a layout-package resource path.
    /// </summary>
    /// <param name="value">Candidate resource path.</param>
    /// <param name="context">Validation context.</param>
    /// <returns>The validation result.</returns>
    public static ValidationResult? ValidateResourcePathValue(string value, ValidationContext context) =>
        value.Length <= FrontedLayoutLimits.MaxResourcePathLength
            ? ValidationResult.Success
            : new ValidationResult(I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "ResourcePathTooLong"));

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
