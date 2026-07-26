using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using System.Text.RegularExpressions;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// v3 前台布局设计期校验器。
/// </summary>
public class FrontedLayoutValidator
{
    private static readonly Regex ValidControlNameRegex = new(
        "^[A-Za-z_][A-Za-z0-9_]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IFrontedV3ControlRegistry? _v3ControlRegistry;
    private readonly IFrontedResourceResolver? _resourceResolver;
    private readonly IFrontedImageSafetyService _imageSafetyService;
    private readonly FrontedLayoutReferenceScanner _referenceScanner;

    /// <summary>
    /// 初始化校验器。
    /// </summary>
    public FrontedLayoutValidator(
        IFrontedV3ControlRegistry? v3ControlRegistry = null,
        IFrontedResourceResolver? resourceResolver = null,
        IFrontedImageSafetyService? imageSafetyService = null,
        FrontedLayoutReferenceScanner? referenceScanner = null)
    {
        _v3ControlRegistry = v3ControlRegistry;
        _resourceResolver = resourceResolver;
        _imageSafetyService = imageSafetyService ?? new FrontedImageSafetyService();
        _referenceScanner = referenceScanner ?? new FrontedLayoutReferenceScanner();
    }

    /// <summary>
    /// 校验运行时 Canvas 配置。
    /// </summary>
    public IReadOnlyList<FrontedLayoutValidationMessage> Validate(
        string windowTypeName,
        string canvasName,
        FrontedCanvasConfig config)
    {
        var converter = _v3ControlRegistry is null
            ? new FrontedLayoutDesignConverter()
            : new FrontedLayoutDesignConverter(_v3ControlRegistry);
        var document = converter
            .FromConfig(windowTypeName, canvasName, config);

        return Validate(document);
    }

    /// <summary>
    /// 校验单 Canvas 设计文档。
    /// </summary>
    public IReadOnlyList<FrontedLayoutValidationMessage> Validate(FrontedCanvasDesignDocument document)
    {
        var messages = new List<FrontedLayoutValidationMessage>();
        ValidateCanvas(document, messages);
        ValidateControlNames(document, messages);
        ValidateControls(document, messages);
        ValidateReferences(document, messages);
        UpdateDesignItemValidationState(document, messages);
        return messages;
    }

    private void ValidateCanvas(
        FrontedCanvasDesignDocument document,
        ICollection<FrontedLayoutValidationMessage> messages)
    {
        if (string.IsNullOrWhiteSpace(document.WindowTypeName))
        {
            messages.Add(Error(
                "RequiredPropertyMissing",
                "WindowTypeName is required.",
                propertyName: nameof(FrontedCanvasDesignDocument.WindowTypeName)));
        }

        if (string.IsNullOrWhiteSpace(document.CanvasName))
        {
            messages.Add(Error(
                "RequiredPropertyMissing",
                "CanvasName is required.",
                propertyName: nameof(FrontedCanvasDesignDocument.CanvasName)));
        }

        if (document.CanvasConfig.Version != 3)
        {
            messages.Add(Error(
                "CanvasVersionInvalid",
                "Canvas layout Version must be 3.",
                propertyName: nameof(FrontedCanvasConfig.Version)));
        }

        if (!IsPositiveFinite(document.CanvasConfig.CanvasWidth))
        {
            messages.Add(Error(
                "CanvasWidthInvalid",
                "CanvasWidth must be a positive number.",
                propertyName: nameof(FrontedCanvasConfig.CanvasWidth)));
        }

        if (!IsPositiveFinite(document.CanvasConfig.CanvasHeight))
        {
            messages.Add(Error(
                "CanvasHeightInvalid",
                "CanvasHeight must be a positive number.",
                propertyName: nameof(FrontedCanvasConfig.CanvasHeight)));
        }

        if (!string.IsNullOrWhiteSpace(document.CanvasConfig.BackgroundImage)
            && _resourceResolver is not null
            && _resourceResolver.ResolveImagePath(document.CanvasConfig.BackgroundImage) is null)
        {
            messages.Add(Warning(
                "BackgroundImageUnresolved",
                $"BackgroundImage '{document.CanvasConfig.BackgroundImage}' could not be resolved.",
                propertyName: nameof(FrontedCanvasConfig.BackgroundImage)));
        }

        if (FrontedTextLimitHelper.IsTooLong(document.CanvasConfig.BackgroundImage, FrontedLayoutLimits.MaxResourcePathLength))
        {
            messages.Add(Error(
                "ResourcePathTooLong",
                "Canvas BackgroundImage is too long.",
                propertyName: nameof(FrontedCanvasConfig.BackgroundImage)));
        }

        if (document.Controls.Count > FrontedLayoutLimits.MaxControlsPerCanvas)
        {
            messages.Add(Error(
                "TooManyControls",
                $"Canvas has {document.Controls.Count} controls; max is {FrontedLayoutLimits.MaxControlsPerCanvas}."));
        }
        else if (document.Controls.Count >= FrontedLayoutLimits.WarningControlsPerCanvas)
        {
            messages.Add(Warning(
                "ControlCountWarning",
                $"Canvas has {document.Controls.Count} controls; warning threshold is {FrontedLayoutLimits.WarningControlsPerCanvas}."));
        }
    }

    private static void ValidateControlNames(
        FrontedCanvasDesignDocument document,
        ICollection<FrontedLayoutValidationMessage> messages)
    {
        var groupedByName = document.Controls
            .Where(control => !string.IsNullOrWhiteSpace(control.Name))
            .GroupBy(control => control.Name, StringComparer.Ordinal);

        foreach (var group in groupedByName.Where(group => group.Count() > 1))
        {
            foreach (var control in group)
            {
                messages.Add(Error(
                    "ControlNameDuplicate",
                    $"Control name '{control.Name}' is duplicated in this canvas.",
                    control.Name,
                    nameof(FrontedControlDesignItem.Name)));
            }
        }

        foreach (var control in document.Controls)
        {
            if (string.IsNullOrWhiteSpace(control.Name))
            {
                messages.Add(Error(
                    "ControlNameEmpty",
                    "Control name cannot be empty.",
                    propertyName: nameof(FrontedControlDesignItem.Name)));
                continue;
            }

            if (FrontedTextLimitHelper.IsTooLong(control.Name, FrontedLayoutLimits.MaxControlNameLength))
            {
                messages.Add(Error(
                    "InputTooLong",
                    $"Control name '{control.Name}' is too long.",
                    control.Name,
                    nameof(FrontedControlDesignItem.Name)));
            }

            if (!ValidControlNameRegex.IsMatch(control.Name))
            {
                messages.Add(Error(
                    "ControlNameInvalid",
                    $"Control name '{control.Name}' must match ^[A-Za-z_][A-Za-z0-9_]*$.",
                    control.Name,
                    nameof(FrontedControlDesignItem.Name)));
            }
        }
    }

    private void ValidateControls(
        FrontedCanvasDesignDocument document,
        ICollection<FrontedLayoutValidationMessage> messages)
    {
        foreach (var item in document.Controls)
        {
            ValidateCommonControlFields(item, messages);
            ValidateKnownControlConfig(item, messages);
            if (item.Config is BackgroundTintFrontedControlConfigBase
                && string.IsNullOrWhiteSpace(document.CanvasConfig.BackgroundImage))
            {
                messages.Add(Info(
                    "MissingCanvasBackgroundImage",
                    $"Background tint control '{item.Name}' has no Canvas background image to tint.",
                    item.Name,
                    nameof(FrontedCanvasConfig.BackgroundImage)));
            }
        }
    }

    private void ValidateCommonControlFields(
        FrontedControlDesignItem item,
        ICollection<FrontedLayoutValidationMessage> messages)
    {
        if (item.Config is null)
        {
            messages.Add(Error(
                "ControlTypeMissing",
                "Control config is missing.",
                item.Name,
                nameof(FrontedControlDesignItem.Config)));
            return;
        }

        if (string.IsNullOrWhiteSpace(item.Config.ControlType))
        {
            messages.Add(Error(
                "ControlTypeMissing",
                $"Control '{item.Name}' is missing ControlType.",
                item.Name,
                nameof(FrontedControlConfigBase.ControlType)));
        }
        else if (_v3ControlRegistry is not null && _v3ControlRegistry.GetRegistration(item.Config.ControlType) is null)
        {
            messages.Add(FrontedPluginControlType.IsPluginControlType(item.Config.ControlType)
                ? Warning(
                    "PluginControlMissing",
                    $"Control '{item.Name}' uses missing plugin ControlType '{item.Config.ControlType}'.",
                    item.Name,
                    nameof(FrontedControlConfigBase.ControlType))
                : Error(
                    "ControlTypeUnknown",
                    $"Control '{item.Name}' has unknown ControlType '{item.Config.ControlType}'.",
                    item.Name,
                    nameof(FrontedControlConfigBase.ControlType)));
        }

        if (FrontedTextLimitHelper.IsTooLong(item.Config.ControlType, FrontedLayoutLimits.MaxControlTypeLength))
        {
            messages.Add(Error(
                "InputTooLong",
                $"Control '{item.Name}' ControlType is too long.",
                item.Name,
                nameof(FrontedControlConfigBase.ControlType)));
        }

        if (FrontedTextLimitHelper.IsTooLong(item.Config.BindingPath, FrontedLayoutLimits.MaxBindingPathLength))
        {
            messages.Add(Error(
                "BindingPathTooLong",
                $"Control '{item.Name}' BindingPath is too long.",
                item.Name,
                nameof(FrontedControlConfigBase.BindingPath)));
        }

        if (!IsFinite(item.Config.Left))
        {
            messages.Add(Error(
                "RequiredPropertyMissing",
                $"Control '{item.Name}' Left must be a finite number.",
                item.Name,
                nameof(FrontedControlConfigBase.Left)));
        }

        if (!IsFinite(item.Config.Top))
        {
            messages.Add(Error(
                "RequiredPropertyMissing",
                $"Control '{item.Name}' Top must be a finite number.",
                item.Name,
                nameof(FrontedControlConfigBase.Top)));
        }

        if (NeedsInteractionSize(item.Config)
            && (!item.Config.Width.HasValue || !item.Config.Height.HasValue))
        {
            messages.Add(Warning(
                "MissingInteractionSize",
                $"Control '{item.Name}' should have Width and Height for editor interaction.",
                item.Name));
        }
    }

    private void ValidateKnownControlConfig(
        FrontedControlDesignItem item,
        ICollection<FrontedLayoutValidationMessage> messages)
    {
        switch (item.Config)
        {
            case TextFrontedControlConfig text:
                ValidateTextLength(item.Name, nameof(TextFrontedControlConfig.Text), text.Text, FrontedLayoutLimits.MaxStaticTextLength, "TextTooLong", messages);
                ValidateTextLength(item.Name, nameof(TextFrontedControlConfig.FontFamily), text.FontFamily, FrontedLayoutLimits.MaxFontFamilyLength, "InputTooLong", messages);
                ValidateTextColorBinding(item.Name, text.Color, text.ColorBindingPath, messages);
                ValidateTextBinding(item.Name, text.TextBinding, messages);
                var hasTextBinding = text.TextBinding?.GetActiveSources().Count > 0;
                if (!hasTextBinding && string.IsNullOrWhiteSpace(text.Text))
                {
                    messages.Add(Warning(
                        "EmptyVisibleContent",
                        $"Text control '{item.Name}' has no TextBinding sources or static Text.",
                        item.Name,
                        nameof(TextFrontedControlConfig.Text)));
                }
                else if (hasTextBinding && !string.IsNullOrWhiteSpace(text.Text))
                {
                    messages.Add(Warning(
                        "StaticTextIgnored",
                        $"Text control '{item.Name}' static Text is ignored because TextBinding has sources.",
                        item.Name,
                        nameof(TextFrontedControlConfig.Text)));
                }

                break;

            case LocalizedTextControlConfig localizedText:
                ValidateTextLength(item.Name, nameof(LocalizedTextControlConfig.FontFamily), localizedText.FontFamily, FrontedLayoutLimits.MaxFontFamilyLength, "InputTooLong", messages);
                ValidateTextColorBinding(item.Name, localizedText.Color, localizedText.ColorBindingPath, messages);
                ValidateTextBinding(item.Name, localizedText.TextBinding, messages);
                var hasLocalizedTextBinding = localizedText.TextBinding?.GetActiveSources().Count > 0;
                if (!hasLocalizedTextBinding && string.IsNullOrWhiteSpace(localizedText.LocalizationKey))
                {
                    messages.Add(Error(
                        "RequiredPropertyMissing",
                        $"LocalizedText control '{item.Name}' requires LocalizationKey or TextBinding sources.",
                        item.Name,
                        nameof(LocalizedTextControlConfig.LocalizationKey)));
                }
                else if (hasLocalizedTextBinding && !string.IsNullOrWhiteSpace(localizedText.LocalizationKey))
                {
                    messages.Add(Warning(
                        "StaticTextIgnored",
                        $"LocalizedText control '{item.Name}' LocalizationKey is ignored because TextBinding has sources.",
                        item.Name,
                        nameof(LocalizedTextControlConfig.LocalizationKey)));
                }

                break;

            case MapNameTextControlConfig mapNameText:
                ValidateTextLength(item.Name, nameof(MapNameTextControlConfig.FontFamily), mapNameText.FontFamily, FrontedLayoutLimits.MaxFontFamilyLength, "InputTooLong", messages);
                ValidateTextColorBinding(item.Name, mapNameText.Color, mapNameText.ColorBindingPath, messages);
                break;

            case ImageFrontedControlConfig image:
                ValidateTextLength(item.Name, nameof(ImageFrontedControlConfig.ImagePath), image.ImagePath, FrontedLayoutLimits.MaxResourcePathLength, "ResourcePathTooLong", messages);
                ValidateTextLength(item.Name, nameof(ImageFrontedControlConfig.PickingBorderImagePath), image.PickingBorderImagePath, FrontedLayoutLimits.MaxResourcePathLength, "ResourcePathTooLong", messages);
                ValidateTextLength(item.Name, nameof(ImageFrontedControlConfig.LockImagePath), image.LockImagePath, FrontedLayoutLimits.MaxResourcePathLength, "ResourcePathTooLong", messages);
                ValidateTextLength(item.Name, nameof(ImageFrontedControlConfig.LockVisibilityBindingPath), image.LockVisibilityBindingPath, FrontedLayoutLimits.MaxBindingPathLength, "BindingPathTooLong", messages);
                ValidateImagePath(item.Name, image);
                if (string.IsNullOrWhiteSpace(image.BindingPath)
                    && string.IsNullOrWhiteSpace(image.ImagePath))
                {
                    messages.Add(Warning(
                        "EmptyVisibleContent",
                        $"Image control '{item.Name}' has no BindingPath or ImagePath.",
                        item.Name,
                        nameof(ImageFrontedControlConfig.ImagePath)));
                }
                else if (!string.IsNullOrWhiteSpace(image.BindingPath)
                         && !string.IsNullOrWhiteSpace(image.ImagePath))
                {
                    messages.Add(Warning(
                        "ImagePathIgnored",
                        $"Image control '{item.Name}' ImagePath is ignored because BindingPath is set.",
                        item.Name,
                        nameof(ImageFrontedControlConfig.ImagePath)));
                }

                break;

            case TalentTraitDisplayControlConfig talent:
                if (!talent.HasValidSurvivorPlayerIndex())
                {
                    messages.Add(Error(
                        "RequiredPropertyMissing",
                        $"TalentTraitDisplay control '{item.Name}' requires PlayerIndex 0..3 for SurvivorTalent.",
                        item.Name,
                        nameof(TalentTraitDisplayControlConfig.PlayerIndex)));
                }

                break;

            case MapV2DisplayControlConfig mapV2:
                ValidateResourceLikeStrings(item.Name, mapV2, messages);
                ValidateTextLength(item.Name, nameof(MapV2DisplayControlConfig.MapNameFontFamily), mapV2.MapNameFontFamily, FrontedLayoutLimits.MaxFontFamilyLength, "InputTooLong", messages);
                ValidateTextLength(item.Name, nameof(MapV2DisplayControlConfig.TeamNameFontFamily), mapV2.TeamNameFontFamily, FrontedLayoutLimits.MaxFontFamilyLength, "InputTooLong", messages);
                ValidateTextLength(item.Name, nameof(MapV2DisplayControlConfig.CampNameFontFamily), mapV2.CampNameFontFamily, FrontedLayoutLimits.MaxFontFamilyLength, "InputTooLong", messages);
                if (string.IsNullOrWhiteSpace(mapV2.MapKey))
                {
                    messages.Add(Error(
                        "RequiredPropertyMissing",
                        $"MapV2Display control '{item.Name}' requires MapKey.",
                        item.Name,
                        nameof(MapV2DisplayControlConfig.MapKey)));
                }

                break;

            case GameProgressTextControlConfig gameProgress:
                ValidateGameProgressText(item.Name, gameProgress, messages);
                break;

            case ShapeFrontedControlConfigBase shape:
                ValidateShape(item.Name, shape, messages);
                break;

            case BackgroundTintFrontedControlConfigBase tint:
                ValidateBackgroundTint(item.Name, tint, messages);
                break;
        }

        static void ValidateTextBinding(
            string controlName,
            Models.FrontedLayout.Binding.FrontedTextBindingExpression? expression,
            ICollection<FrontedLayoutValidationMessage> messages)
        {
            if (expression is null)
            {
                return;
            }

            foreach (var source in expression.Sources)
            {
                if (string.IsNullOrWhiteSpace(source.Path))
                {
                    messages.Add(Error(
                        "TextBindingSourcePathEmpty",
                        $"TextBinding source in control '{controlName}' has an empty Path.",
                        controlName,
                        nameof(TextFrontedControlConfig.TextBinding)));
                }
                else if (FrontedTextLimitHelper.IsTooLong(source.Path, FrontedLayoutLimits.MaxBindingPathLength))
                {
                    messages.Add(Error(
                        "BindingPathTooLong",
                        $"TextBinding source '{source.Path}' in control '{controlName}' is too long.",
                        controlName,
                        nameof(TextFrontedControlConfig.TextBinding)));
                }
            }

            if (!FrontedTextBindingHelper.TryValidateStringFormat(
                    expression.StringFormat,
                    expression.GetActiveSources().Count,
                    out var formatError))
            {
                messages.Add(Error(
                    "TextBindingStringFormatInvalid",
                    $"TextBinding StringFormat in control '{controlName}' is invalid: {formatError}",
                    controlName,
                    nameof(TextFrontedControlConfig.TextBinding)));
            }
        }

        void ValidateImagePath(string controlName, ImageFrontedControlConfig config)
        {
            if (string.IsNullOrWhiteSpace(config.ImagePath) || _resourceResolver is null)
            {
                return;
            }

            var resolvedPath = _resourceResolver.ResolveImagePath(config.ImagePath);
            if (resolvedPath is null)
            {
                messages.Add(Warning(
                    "ImagePathUnresolved",
                    $"ImagePath '{config.ImagePath}' could not be resolved.",
                    controlName,
                    nameof(ImageFrontedControlConfig.ImagePath)));
                return;
            }

            var validation = _imageSafetyService.ValidateFile(resolvedPath, FrontedImagePurpose.UiElement);
            if (!validation.IsValid)
            {
                messages.Add(Warning(
                    "ImagePathUnsafe",
                    $"ImagePath '{config.ImagePath}' was rejected by image safety validation: {validation.ErrorCode}.",
                    controlName,
                    nameof(ImageFrontedControlConfig.ImagePath)));
            }
        }
    }

    private static void ValidateShape(
        string controlName,
        ShapeFrontedControlConfigBase shape,
        ICollection<FrontedLayoutValidationMessage> messages)
    {
        ValidateTextLength(controlName, nameof(shape.FillBindingPath), shape.FillBindingPath, FrontedLayoutLimits.MaxBindingPathLength, "BindingPathTooLong", messages);
        ValidateTextLength(controlName, nameof(shape.GradientStartBindingPath), shape.GradientStartBindingPath, FrontedLayoutLimits.MaxBindingPathLength, "BindingPathTooLong", messages);
        ValidateTextLength(controlName, nameof(shape.GradientEndBindingPath), shape.GradientEndBindingPath, FrontedLayoutLimits.MaxBindingPathLength, "BindingPathTooLong", messages);

        var useGradient = shape.UseGradient || shape.FillMode == ShapeFillMode.LinearGradient;

        var hasFillBinding = !string.IsNullOrWhiteSpace(shape.FillBindingPath);
        var hasGradientEndBinding = !string.IsNullOrWhiteSpace(shape.GradientEndBindingPath);

        ValidateColor(nameof(shape.StrokeColor), shape.StrokeColor, false);

        // FillColor: required when no binding; warn when binding overrides it
        ValidateColor(nameof(shape.FillColor), shape.FillColor, !hasFillBinding);
        if (hasFillBinding && !string.IsNullOrWhiteSpace(shape.FillColor))
        {
            messages.Add(Warning(
                "FillColorIgnored",
                $"Shape '{controlName}' FillColor is ignored while binding is active.",
                controlName,
                nameof(shape.FillColor)));
        }

        if (useGradient)
        {
            // GradientEndColor: required when no binding; warn when binding overrides it
            ValidateColor(nameof(shape.GradientEndColor), shape.GradientEndColor, !hasGradientEndBinding);
            if (hasGradientEndBinding && !string.IsNullOrWhiteSpace(shape.GradientEndColor))
            {
                messages.Add(Warning(
                    "GradientEndColorIgnored",
                    $"Shape '{controlName}' GradientEndColor is ignored while binding is active.",
                    controlName,
                    nameof(shape.GradientEndColor)));
            }

            if (!double.IsFinite(shape.GradientAngle))
            {
                messages.Add(Error("ShapeGradientAngleInvalid", $"Control '{controlName}' GradientAngle must be finite.", controlName, nameof(shape.GradientAngle)));
            }
        }

        if (!double.IsFinite(shape.StrokeThickness) || shape.StrokeThickness < 0)
        {
            messages.Add(Error("ShapeStrokeThicknessInvalid", $"Control '{controlName}' StrokeThickness must be a non-negative finite number.", controlName, nameof(shape.StrokeThickness)));
        }

        if (shape is PolygonFrontedControlConfig polygon)
        {
            if (polygon.Points is null || polygon.Points.Count < 3)
            {
                messages.Add(Error("PolygonPointsTooFew", $"Polygon control '{controlName}' requires at least three points.", controlName, nameof(polygon.Points)));
            }
            else if (polygon.Points.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y)))
            {
                messages.Add(Error("PolygonPointInvalid", $"Polygon control '{controlName}' contains a non-finite point.", controlName, nameof(polygon.Points)));
            }
        }

        void ValidateColor(string propertyName, string? value, bool required)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                if (required)
                {
                    messages.Add(Error("RequiredPropertyMissing", $"Control '{controlName}' requires {propertyName}.", controlName, propertyName));
                }

                return;
            }

            if (!ColorHelper.TryNormalizeHex(value, out _))
            {
                messages.Add(Error("InvalidColorHex", $"Control '{controlName}' {propertyName} must be #RRGGBB or #AARRGGBB.", controlName, propertyName));
            }
        }
    }

    private static void ValidateBackgroundTint(
        string controlName,
        BackgroundTintFrontedControlConfigBase tint,
        ICollection<FrontedLayoutValidationMessage> messages)
    {
        ValidateTextLength(
            controlName,
            nameof(tint.TintBindingPath),
            tint.TintBindingPath,
            FrontedLayoutLimits.MaxBindingPathLength,
            "BindingPathTooLong",
            messages);

        if (!Enum.IsDefined(tint.TintMode))
        {
            messages.Add(Error(
                "RequiredPropertyMissing",
                $"Background tint control '{controlName}' has invalid TintMode.",
                controlName,
                nameof(tint.TintMode)));
        }

        if (string.IsNullOrWhiteSpace(tint.TintBindingPath)
            && !ColorHelper.TryNormalizeHex(tint.TintColor, out _))
        {
            messages.Add(Error(
                "InvalidColorHex",
                $"Background tint control '{controlName}' TintColor must be #RRGGBB or #AARRGGBB.",
                controlName,
                nameof(tint.TintColor)));
        }
        else if (!string.IsNullOrWhiteSpace(tint.TintBindingPath)
                 && !string.IsNullOrWhiteSpace(tint.TintColor))
        {
            messages.Add(Warning(
                "TintColorIgnored",
                $"Background tint control '{controlName}' TintColor is ignored while binding is active.",
                controlName,
                nameof(tint.TintColor)));
        }

        if (!double.IsFinite(tint.TintStrength))
        {
            messages.Add(Error(
                "TintStrengthInvalid",
                $"Background tint control '{controlName}' TintStrength must be finite.",
                controlName,
                nameof(tint.TintStrength)));
        }
        else if (tint.TintStrength is < 0D or > 1D)
        {
            messages.Add(Warning(
                "TintStrengthClamped",
                $"Background tint control '{controlName}' TintStrength will be clamped to 0..1.",
                controlName,
                nameof(tint.TintStrength)));
        }

        if (!double.IsFinite(tint.TextureStrength))
        {
            messages.Add(Error(
                "TextureStrengthInvalid",
                $"Background tint control '{controlName}' TextureStrength must be finite.",
                controlName,
                nameof(tint.TextureStrength)));
        }
        else if (tint.TextureStrength is < 0D or > 1D)
        {
            messages.Add(Warning(
                "TextureStrengthClamped",
                $"Background tint control '{controlName}' TextureStrength will be clamped to 0..1.",
                controlName,
                nameof(tint.TextureStrength)));
        }

        if (tint is BackgroundTintPolygonFrontedControlConfig polygon)
        {
            if (polygon.Points.Count < 3)
            {
                messages.Add(Error(
                    "PolygonPointsTooFew",
                    $"Background tint polygon '{controlName}' requires at least three points.",
                    controlName,
                    nameof(polygon.Points)));
            }
            else if (polygon.Points.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y)))
            {
                messages.Add(Error(
                    "PolygonPointInvalid",
                    $"Background tint polygon '{controlName}' contains a non-finite point.",
                    controlName,
                    nameof(polygon.Points)));
            }
        }
    }

    private static void ValidateGameProgressText(
        string controlName,
        GameProgressTextControlConfig config,
        ICollection<FrontedLayoutValidationMessage> messages)
    {
        ValidateTextColorBinding(controlName, config.Color, config.ColorBindingPath, messages);

        if (config.FontSize <= 0)
        {
            messages.Add(Error(
                "RequiredPropertyMissing",
                $"Control '{controlName}' FontSize must be greater than 0.",
                controlName,
                nameof(config.FontSize)));
        }

        if (config.VerticalTextSpacing < 0)
        {
            messages.Add(Error(
                "RequiredPropertyMissing",
                $"Control '{controlName}' VerticalTextSpacing must be >= 0.",
                controlName,
                nameof(config.VerticalTextSpacing)));
        }

        if (config.GroupSpacing < 0)
        {
            messages.Add(Error(
                "RequiredPropertyMissing",
                $"Control '{controlName}' GroupSpacing must be >= 0.",
                controlName,
                nameof(config.GroupSpacing)));
        }

        if (config.SeparatorThickness < 0)
        {
            messages.Add(Error(
                "RequiredPropertyMissing",
                $"Control '{controlName}' SeparatorThickness must be >= 0.",
                controlName,
                nameof(config.SeparatorThickness)));
        }

        if (config.PaddingLeft < 0 || config.PaddingTop < 0 || config.PaddingRight < 0 || config.PaddingBottom < 0)
        {
            messages.Add(Error(
                "RequiredPropertyMissing",
                $"Control '{controlName}' Padding values must be >= 0.",
                controlName,
                "Padding"));
        }

        if (!string.IsNullOrWhiteSpace(config.SeparatorColor)
            && !ColorHelper.TryNormalizeHex(config.SeparatorColor, out _))
        {
            messages.Add(Error(
                "InvalidColorHex",
                $"Control '{controlName}' SeparatorColor must be #RRGGBB or #AARRGGBB.",
                controlName,
                nameof(config.SeparatorColor)));
        }

        if (!string.IsNullOrWhiteSpace(config.BackgroundColor)
            && !ColorHelper.TryNormalizeHex(config.BackgroundColor, out _))
        {
            messages.Add(Error(
                "InvalidColorHex",
                $"Control '{controlName}' BackgroundColor must be #RRGGBB or #AARRGGBB.",
                controlName,
                nameof(config.BackgroundColor)));
        }

        if (!Enum.IsDefined(config.DisplayMode))
        {
            messages.Add(Error(
                "RequiredPropertyMissing",
                $"Control '{controlName}' has invalid DisplayMode.",
                controlName,
                nameof(config.DisplayMode)));
        }

        if (!Enum.IsDefined(config.NumberStyle))
        {
            messages.Add(Error(
                "RequiredPropertyMissing",
                $"Control '{controlName}' has invalid NumberStyle.",
                controlName,
                nameof(config.NumberStyle)));
        }

        if (!Enum.IsDefined(config.LatinVerticalMode))
        {
            messages.Add(Error(
                "RequiredPropertyMissing",
                $"Control '{controlName}' has invalid LatinVerticalMode.",
                controlName,
                nameof(config.LatinVerticalMode)));
        }
    }

    private static void ValidateTextColorBinding(
        string controlName,
        string? color,
        string? colorBindingPath,
        ICollection<FrontedLayoutValidationMessage> messages)
    {
        ValidateTextLength(
            controlName,
            nameof(TextFrontedControlConfig.ColorBindingPath),
            colorBindingPath,
            FrontedLayoutLimits.MaxBindingPathLength,
            "BindingPathTooLong",
            messages);

        if (!string.IsNullOrWhiteSpace(colorBindingPath)
            && !string.IsNullOrWhiteSpace(color))
        {
            messages.Add(Warning(
                "TextColorIgnored",
                $"Text control '{controlName}' Color is ignored while binding is active.",
                controlName,
                nameof(TextFrontedControlConfig.Color)));
        }
    }

    private static void ValidateResourceLikeStrings(
        string controlName,
        object config,
        ICollection<FrontedLayoutValidationMessage> messages)
    {
        foreach (var property in config.GetType().GetProperties())
        {
            if (property.PropertyType != typeof(string))
            {
                continue;
            }

            if (property.Name.Contains("Image", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Path", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Source", StringComparison.OrdinalIgnoreCase))
            {
                ValidateTextLength(
                    controlName,
                    property.Name,
                    property.GetValue(config) as string,
                    FrontedLayoutLimits.MaxResourcePathLength,
                    "ResourcePathTooLong",
                    messages);
            }
        }
    }

    private static void ValidateTextLength(
        string controlName,
        string propertyName,
        string? value,
        int maxLength,
        string code,
        ICollection<FrontedLayoutValidationMessage> messages)
    {
        if (FrontedTextLimitHelper.IsTooLong(value, maxLength))
        {
            messages.Add(Error(
                code,
                $"Control '{controlName}' {propertyName} is too long.",
                controlName,
                propertyName));
        }
    }

    private void ValidateReferences(
        FrontedCanvasDesignDocument document,
        ICollection<FrontedLayoutValidationMessage> messages)
    {
        _referenceScanner.SetControls(document.Controls);
        var controlNames = document.Controls
            .Where(control => !string.IsNullOrWhiteSpace(control.Name))
            .Select(control => control.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var reference in _referenceScanner.GetReferences(document.Controls))
        {
            if (!controlNames.Contains(reference.TargetControlName))
            {
                messages.Add(Error(
                    "ReferenceTargetMissing",
                    $"Control '{reference.SourceControlName}' references missing target '{reference.TargetControlName}'.",
                    reference.SourceControlName,
                    reference.PropertyName));
            }
        }
    }

    private static void UpdateDesignItemValidationState(
        FrontedCanvasDesignDocument document,
        IReadOnlyList<FrontedLayoutValidationMessage> messages)
    {
        foreach (var item in document.Controls)
        {
            item.ValidationMessages = messages
                .Where(message => message.ControlName == item.Name)
                .ToArray();
        }
    }

    private static void ValidateEnumValue<TEnum>(
        string controlName,
        string propertyName,
        TEnum value,
        ICollection<FrontedLayoutValidationMessage> messages)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            messages.Add(Error(
                "RequiredPropertyMissing",
                $"Control '{controlName}' has invalid {propertyName}.",
                controlName,
                propertyName));
        }
    }

    private static void ValidateNonNegativeIndex(
        string controlName,
        int index,
        ICollection<FrontedLayoutValidationMessage> messages)
    {
        if (index < 0)
        {
            messages.Add(Error(
                "RequiredPropertyMissing",
                $"Control '{controlName}' Index must be >= 0.",
                controlName,
                "Index"));
        }
    }

    private static bool NeedsInteractionSize(FrontedControlConfigBase config)
    {
        return config is ImageFrontedControlConfig
            or ShapeFrontedControlConfigBase
            or BackgroundTintFrontedControlConfigBase
            or TalentTraitDisplayControlConfig
            or MapV2DisplayControlConfig;
    }

    private static bool IsPositiveFinite(double value) => IsFinite(value) && value > 0;

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private static FrontedLayoutValidationMessage Error(
        string code,
        string message,
        string? controlName = null,
        string? propertyName = null)
    {
        return Create(FrontedLayoutValidationSeverity.Error, code, message, controlName, propertyName);
    }

    private static FrontedLayoutValidationMessage Warning(
        string code,
        string message,
        string? controlName = null,
        string? propertyName = null)
    {
        return Create(FrontedLayoutValidationSeverity.Warning, code, message, controlName, propertyName);
    }

    private static FrontedLayoutValidationMessage Info(
        string code,
        string message,
        string? controlName = null,
        string? propertyName = null)
    {
        return Create(FrontedLayoutValidationSeverity.Info, code, message, controlName, propertyName);
    }

    private static FrontedLayoutValidationMessage Create(
        FrontedLayoutValidationSeverity severity,
        string code,
        string message,
        string? controlName = null,
        string? propertyName = null)
    {
        return new FrontedLayoutValidationMessage
        {
            Severity = severity,
            Code = code,
            Message = message,
            ControlName = controlName,
            PropertyName = propertyName
        };
    }
}
