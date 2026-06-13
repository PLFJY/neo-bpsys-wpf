using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;
using System.Collections;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Builds Designer v3 property grid rows for the selected design item.
/// </summary>
public class FrontedPropertyGridBuilder
{
    private readonly FrontedFontFamilyOptionProvider _fontFamilyOptionProvider;
    private readonly IFrontedDesignerLocalizationService _localizationService;
    private readonly IFrontedControlRegistry? _controlRegistry;

    /// <summary>
    /// Initializes a property grid builder with default font options.
    /// </summary>
    public FrontedPropertyGridBuilder()
        : this(new FrontedFontFamilyOptionProvider(), new FrontedDesignerLocalizationService(), null)
    {
    }

    /// <summary>
    /// Initializes a property grid builder with a custom font option provider.
    /// </summary>
    public FrontedPropertyGridBuilder(FrontedFontFamilyOptionProvider fontFamilyOptionProvider)
        : this(fontFamilyOptionProvider, new FrontedDesignerLocalizationService(), null)
    {
    }

    /// <summary>
    /// Initializes a property grid builder with custom font options and localization.
    /// </summary>
    public FrontedPropertyGridBuilder(
        FrontedFontFamilyOptionProvider fontFamilyOptionProvider,
        IFrontedDesignerLocalizationService localizationService,
        IFrontedControlRegistry? controlRegistry = null)
    {
        _fontFamilyOptionProvider = fontFamilyOptionProvider;
        _localizationService = localizationService;
        _controlRegistry = controlRegistry;
    }

    private static readonly HashSet<string> CommonPropertyNames = new(StringComparer.Ordinal)
    {
        nameof(FrontedControlConfigBase.Left),
        nameof(FrontedControlConfigBase.Top),
        nameof(FrontedControlConfigBase.Width),
        nameof(FrontedControlConfigBase.Height),
        nameof(FrontedControlConfigBase.ZIndex),
        nameof(FrontedControlConfigBase.BindingPath)
    };

    private static readonly HashSet<string> ColorPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Color",
        "Foreground",
        "Background",
        "FillColor",
        "BorderColor"
    };

    private static readonly HashSet<string> AppearancePropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Color",
        "Foreground",
        "Background",
        "FillColor",
        "BorderColor",
        "FontFamily",
        "FontWeight",
        "FontSize",
        "HorizontalAlignment",
        "VerticalAlignment",
        "TextAlignment",
        "TextWrapping",
        "Stretch",
        "SizingMode",
        "CornerRadius",
        "ClipToBounds"
    };

    private static readonly HashSet<string> ResourcePathPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "ImagePath",
        "ImageSource",
        "SourcePath",
        "ResourcePath",
        "BackgroundImage",
        "LockImageSource",
        "LockImagePath",
        "BorderImagePath",
        "PickingBorderImagePath",
        "BanLockImagePath"
    };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<object>> StringOptionProperties =
        new Dictionary<string, IReadOnlyList<object>>(StringComparer.OrdinalIgnoreCase)
        {
            ["HorizontalAlignment"] = ["Left", "Center", "Right", "Stretch"],
            ["VerticalAlignment"] = ["Top", "Center", "Bottom", "Stretch"],
            ["TextAlignment"] = ["Left", "Center", "Right", "Justify"],
            ["TextWrapping"] = ["NoWrap", "Wrap", "WrapWithOverflow"],
            ["Stretch"] = ["None", "Fill", "Uniform", "UniformToFill"],
            ["FontWeight"] = ["Normal", "Bold", "SemiBold", "Light", "Medium", "ExtraBold"]
        };

    /// <summary>
    /// Builds property editor rows for the selected design item.
    /// </summary>
    public ObservableCollection<FrontedPropertyEditorItem> Build(
        FrontedCanvasDesignDocument document,
        FrontedControlDesignItem selectedItem,
        FrontedLayoutValidator validator,
        FrontedLayoutReferenceScanner referenceScanner,
        FrontedLayoutRuntimeContractCatalog runtimeContractCatalog)
    {
        var messages = validator.Validate(document);
        referenceScanner.SetControls(document.Controls);
        selectedItem.IsRuntimeCritical = runtimeContractCatalog.IsRuntimeCritical(
            document.WindowTypeName,
            document.CanvasName,
            selectedItem.Name);

        var rows = new List<FrontedPropertyEditorItem>();
        AddIdentityRows(rows, selectedItem, messages);
        AddConfigRows(rows, selectedItem, messages);
        MarkGroupHeaders(rows);
        return new ObservableCollection<FrontedPropertyEditorItem>(rows);
    }

    private void AddIdentityRows(
        ICollection<FrontedPropertyEditorItem> rows,
        FrontedControlDesignItem selectedItem,
        IReadOnlyList<FrontedLayoutValidationMessage> messages)
    {
        var nameReadOnly = selectedItem.IsRuntimeCritical
                           || !selectedItem.IsSelectableInEditor
                           || !selectedItem.IsEditableInEditor;

        rows.Add(new FrontedPropertyEditorItem
        {
            DisplayName = _localizationService.GetPropertyDisplayName(nameof(FrontedControlDesignItem.Name)),
            PropertyName = nameof(FrontedControlDesignItem.Name),
            Description = _localizationService.GetPropertyDescription(nameof(FrontedControlDesignItem.Name)),
            PropertyType = typeof(string),
            EditorKind = nameReadOnly ? FrontedPropertyEditorKind.ReadOnly : FrontedPropertyEditorKind.Text,
            Value = selectedItem.Name,
            DisplayValue = selectedItem.Name,
            EditText = selectedItem.Name,
            IsReadOnly = nameReadOnly,
            IsRequired = true,
            GroupName = "Identity",
            ValidationErrors = GetPropertyMessages(messages, selectedItem.Name, nameof(FrontedControlDesignItem.Name)),
            ValidationMessages = GetPropertyValidationMessages(messages, selectedItem.Name, nameof(FrontedControlDesignItem.Name))
        });

        rows.Add(new FrontedPropertyEditorItem
        {
            DisplayName = _localizationService.GetPropertyDisplayName(nameof(FrontedControlConfigBase.ControlType)),
            PropertyName = nameof(FrontedControlConfigBase.ControlType),
            PropertyType = typeof(string),
            EditorKind = FrontedPropertyEditorKind.ReadOnly,
            Value = selectedItem.Config.ControlType,
            DisplayValue = _localizationService.GetControlTypeDisplayName(selectedItem.Config.ControlType),
            IsReadOnly = true,
            IsRequired = true,
            GroupName = "Identity",
            ValidationErrors = GetPropertyMessages(messages, selectedItem.Name, nameof(FrontedControlConfigBase.ControlType)),
            ValidationMessages = GetPropertyValidationMessages(messages, selectedItem.Name, nameof(FrontedControlConfigBase.ControlType))
        });

        rows.Add(new FrontedPropertyEditorItem
        {
            DisplayName = _localizationService.GetPropertyDisplayName("RuntimeCritical"),
            PropertyName = "RuntimeCritical",
            PropertyType = typeof(bool),
            EditorKind = FrontedPropertyEditorKind.ReadOnly,
            Value = selectedItem.IsRuntimeCritical,
            DisplayValue = GetDisplayValue(selectedItem.IsRuntimeCritical, isReadOnly: true),
            IsReadOnly = true,
            GroupName = "Identity"
        });

    }

    private void AddConfigRows(
        ICollection<FrontedPropertyEditorItem> rows,
        FrontedControlDesignItem selectedItem,
        IReadOnlyList<FrontedLayoutValidationMessage> messages)
    {
        var pluginDescriptor = _controlRegistry?.GetPluginDescriptor(selectedItem.Config.ControlType);
        if (selectedItem.Config is PluginFrontedControlConfig missingPlugin && pluginDescriptor is null)
        {
            AddMissingPluginRows(rows, selectedItem, missingPlugin, messages);
            return;
        }

        if (pluginDescriptor is not null && pluginDescriptor.Properties?.Count > 0)
        {
            AddPluginMetadataRows(rows, selectedItem, pluginDescriptor, messages);
            return;
        }

        var properties = selectedItem.Config.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(IsSupportedProperty)
            .OrderBy(GetPropertyOrder);

        foreach (var property in properties)
        {
            if (property.Name is nameof(FrontedControlConfigBase.ControlType)
                or nameof(FrontedControlConfigBase.BehaviorGuid))
            {
                continue;
            }

            if (!IsVisibleProperty(selectedItem.Config, property.Name))
            {
                continue;
            }

            var kind = ResolveEditorKind(property);
            var isReadOnly = !selectedItem.IsEditableInEditor || !property.CanWrite;
            var groupName = ResolveGroupName(property.Name, selectedItem.Config);
            var validationMessages = GetPropertyValidationMessages(messages, selectedItem.Name, property.Name).ToList();
            var validationErrors = validationMessages.Select(message => message.Message).ToList();
            var value = property.GetValue(selectedItem.Config);

            if (kind == FrontedPropertyEditorKind.Color
                && value is string color
                && !string.IsNullOrWhiteSpace(color)
                && !FrontedPropertyColorHelper.TryParseArgbColor(color, out _))
            {
                var message = _localizationService.GetDesignerText(
                    "Designer.Validation.InvalidArgbColor",
                    "Invalid color. Use #RRGGBB, #AARRGGBB, or a WPF color name.");
                validationErrors.Add(message);
                validationMessages.Add(CreatePropertyError(message, selectedItem.Name, property.Name));
            }

            var canBrowseBinding = !isReadOnly && IsBindingPathProperty(property.Name);
            var canBrowseResource = !isReadOnly
                                    && !canBrowseBinding
                                    && IsResourcePathProperty(property.Name);
            var bindingTargetKind = canBrowseBinding
                ? ResolveBindingTargetKind(selectedItem.Config, property)
                : FrontedBindingTargetKind.Any;

            rows.Add(new FrontedPropertyEditorItem
            {
                DisplayName = _localizationService.GetPropertyDisplayName(property.Name),
                PropertyName = property.Name,
                Description = NullIfEmpty(_localizationService.GetPropertyDescription(property.Name)),
                PropertyType = property.PropertyType,
                EditorKind = isReadOnly ? FrontedPropertyEditorKind.ReadOnly : kind,
                Value = value,
                DisplayValue = GetDisplayValue(value, isReadOnly),
                EditText = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                IsReadOnly = isReadOnly,
                IsRequired = property.Name is nameof(FrontedControlConfigBase.Left)
                    or nameof(FrontedControlConfigBase.Top),
                Options = ResolveOptions(property, kind),
                GroupName = groupName,
                ValidationErrors = validationErrors,
                ValidationMessages = validationMessages,
                CanBrowseBinding = canBrowseBinding,
                CanBrowseResource = canBrowseResource,
                BrowseButtonText = "...",
                BrowseDialogTitle = canBrowseBinding
                    ? _localizationService.GetDesignerText("Designer.Editor.BindingBrowser", "Binding Browser")
                    : canBrowseResource
                        ? _localizationService.GetDesignerText("Designer.Editor.ResourceBrowser", "Resource Browser")
                        : null,
                BindingTargetKind = bindingTargetKind,
                ExpectedBindingTypeName = _localizationService.GetBindingTypeDisplayName(ResolveBindingTargetTypeName(bindingTargetKind)),
                AllowedBindingTypeNames = ResolveAllowedBindingTypeNames(bindingTargetKind)
            });
        }
    }

    private void AddPluginMetadataRows(
        ICollection<FrontedPropertyEditorItem> rows,
        FrontedControlDesignItem selectedItem,
        IFrontedPluginControlDescriptor descriptor,
        IReadOnlyList<FrontedLayoutValidationMessage> messages)
    {
        var added = new HashSet<string>(StringComparer.Ordinal);
        foreach (var propertyName in new[]
                 {
                     nameof(FrontedControlConfigBase.Left),
                     nameof(FrontedControlConfigBase.Top),
                     nameof(FrontedControlConfigBase.Width),
                     nameof(FrontedControlConfigBase.Height),
                     nameof(FrontedControlConfigBase.ZIndex),
                     nameof(FrontedControlConfigBase.BindingPath)
                 })
        {
            var property = selectedItem.Config.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property is not null && IsSupportedProperty(property))
            {
                AddPropertyRow(rows, selectedItem, messages, property, null);
                added.Add(property.Name);
            }
        }

        foreach (var metadata in descriptor.Properties?.Where(property => property.IsVisible) ?? [])
        {
            if (!added.Add(metadata.PropertyName))
            {
                continue;
            }

            var property = selectedItem.Config.GetType().GetProperty(metadata.PropertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property is null || !IsSupportedProperty(property))
            {
                continue;
            }

            AddPropertyRow(rows, selectedItem, messages, property, metadata);
        }
    }

    private void AddMissingPluginRows(
        ICollection<FrontedPropertyEditorItem> rows,
        FrontedControlDesignItem selectedItem,
        PluginFrontedControlConfig config,
        IReadOnlyList<FrontedLayoutValidationMessage> messages)
    {
        foreach (var propertyName in new[]
                 {
                     nameof(FrontedControlConfigBase.Left),
                     nameof(FrontedControlConfigBase.Top),
                     nameof(FrontedControlConfigBase.Width),
                     nameof(FrontedControlConfigBase.Height),
                     nameof(FrontedControlConfigBase.ZIndex),
                     nameof(FrontedControlConfigBase.BindingPath)
                 })
        {
            var property = selectedItem.Config.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property is not null && IsSupportedProperty(property))
            {
                AddPropertyRow(rows, selectedItem, messages, property, null);
            }
        }

        AddReadOnlyInfoRow(rows, "PackageId", config.PackageId, "Plugin");
        AddReadOnlyInfoRow(rows, "ControlTypeName", config.ControlTypeName, "Plugin");
        if (config.ExtensionData.Count > 0)
        {
            AddReadOnlyInfoRow(rows, "PluginExtensionData", string.Join(", ", config.ExtensionData.Keys.OrderBy(key => key, StringComparer.Ordinal)), "Plugin");
        }

        AddReadOnlyInfoRow(
            rows,
            "PluginInstallGuidance",
            _localizationService.GetDesignerText(
                "Designer.PluginInstallGuidance",
                "This plugin is not installed. Install guidance will be available in a future version."),
            "Plugin");
    }

    private void AddReadOnlyInfoRow(
        ICollection<FrontedPropertyEditorItem> rows,
        string propertyName,
        object? value,
        string groupName)
    {
        rows.Add(new FrontedPropertyEditorItem
        {
            DisplayName = _localizationService.GetPropertyDisplayName(propertyName),
            PropertyName = propertyName,
            PropertyType = typeof(string),
            EditorKind = FrontedPropertyEditorKind.ReadOnly,
            Value = value,
            DisplayValue = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            IsReadOnly = true,
            GroupName = groupName
        });
    }

    private void AddPropertyRow(
        ICollection<FrontedPropertyEditorItem> rows,
        FrontedControlDesignItem selectedItem,
        IReadOnlyList<FrontedLayoutValidationMessage> messages,
        PropertyInfo property,
        FrontedPluginPropertyDescriptor? metadata)
    {
        var kind = metadata?.EditorKind ?? ResolveEditorKind(property);
        var isReadOnly = !selectedItem.IsEditableInEditor || !property.CanWrite || metadata?.IsReadOnly == true;
        var groupName = metadata?.GroupName ?? ResolveGroupName(property.Name, selectedItem.Config);
        var validationMessages = GetPropertyValidationMessages(messages, selectedItem.Name, property.Name).ToList();
        var validationErrors = validationMessages.Select(message => message.Message).ToList();
        var value = property.GetValue(selectedItem.Config);

        if (kind == FrontedPropertyEditorKind.Color
            && value is string color
            && !string.IsNullOrWhiteSpace(color)
            && !FrontedPropertyColorHelper.TryParseArgbColor(color, out _))
        {
            var message = _localizationService.GetDesignerText(
                "Designer.Validation.InvalidArgbColor",
                "Invalid color. Use #RRGGBB, #AARRGGBB, or a WPF color name.");
            validationErrors.Add(message);
            validationMessages.Add(CreatePropertyError(message, selectedItem.Name, property.Name));
        }

        var canBrowseBinding = !isReadOnly && IsBindingPathProperty(property.Name);
        var canBrowseResource = !isReadOnly
                                && !canBrowseBinding
                                && IsResourcePathProperty(property.Name);
        var bindingTargetKind = canBrowseBinding
            ? metadata?.BindingTargetKind ?? ResolveBindingTargetKind(selectedItem.Config, property)
            : FrontedBindingTargetKind.Any;

        rows.Add(new FrontedPropertyEditorItem
        {
            DisplayName = ResolveMetadataText(metadata?.DisplayNameKey, _localizationService.GetPropertyDisplayName(property.Name)),
            PropertyName = property.Name,
            Description = ResolveMetadataText(metadata?.DescriptionKey, NullIfEmpty(_localizationService.GetPropertyDescription(property.Name)) ?? string.Empty),
            PropertyType = property.PropertyType,
            EditorKind = isReadOnly ? FrontedPropertyEditorKind.ReadOnly : kind,
            Value = value,
            DisplayValue = GetDisplayValue(value, isReadOnly),
            EditText = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            IsReadOnly = isReadOnly,
            IsRequired = property.Name is nameof(FrontedControlConfigBase.Left)
                or nameof(FrontedControlConfigBase.Top),
            Options = metadata?.Options?.Cast<object>().ToArray() ?? ResolveOptions(property, kind),
            GroupName = groupName,
            ValidationErrors = validationErrors,
            ValidationMessages = validationMessages,
            CanBrowseBinding = canBrowseBinding,
            CanBrowseResource = canBrowseResource,
            BrowseButtonText = "...",
            BrowseDialogTitle = canBrowseBinding
                ? _localizationService.GetDesignerText("Designer.Editor.BindingBrowser", "Binding Browser")
                : canBrowseResource
                    ? _localizationService.GetDesignerText("Designer.Editor.ResourceBrowser", "Resource Browser")
                    : null,
            BindingTargetKind = bindingTargetKind,
            ExpectedBindingTypeName = _localizationService.GetBindingTypeDisplayName(ResolveBindingTargetTypeName(bindingTargetKind)),
            AllowedBindingTypeNames = ResolveAllowedBindingTypeNames(bindingTargetKind)
        });
    }

    private string ResolveMetadataText(string? key, string fallback) =>
        string.IsNullOrWhiteSpace(key) ? fallback : _localizationService.GetDesignerText(key, fallback);

    private static FrontedBindingTargetKind ResolveBindingTargetKind(
        FrontedControlConfigBase config,
        PropertyInfo property)
    {
        if (!IsBindingPathProperty(property.Name))
        {
            return FrontedBindingTargetKind.Any;
        }

        if (property.Name == nameof(ImageFrontedControlConfig.LockVisibilityBindingPath))
        {
            return FrontedBindingTargetKind.Boolean;
        }

        if (config is ShapeFrontedControlConfigBase)
        {
            return FrontedBindingTargetKind.String;
        }

        if (config is BackgroundTintFrontedControlConfigBase)
        {
            return FrontedBindingTargetKind.String;
        }

        return config switch
        {
            TextFrontedControlConfig => FrontedBindingTargetKind.Text,
            LocalizedTextControlConfig => FrontedBindingTargetKind.Text,
            ImageFrontedControlConfig => FrontedBindingTargetKind.Image,
            GameProgressTextControlConfig => FrontedBindingTargetKind.GameProgress,
            MapNameTextControlConfig => FrontedBindingTargetKind.Map,
            _ => FrontedBindingTargetKind.Any
        };
    }

    private static string ResolveBindingTargetTypeName(FrontedBindingTargetKind kind)
    {
        return kind switch
        {
            FrontedBindingTargetKind.Text => "Text",
            FrontedBindingTargetKind.Image => "ImageSource",
            FrontedBindingTargetKind.GameProgress => "GameProgress",
            FrontedBindingTargetKind.Map => "Map",
            FrontedBindingTargetKind.Boolean => "Boolean",
            FrontedBindingTargetKind.Number => "Number",
            FrontedBindingTargetKind.String => "String",
            FrontedBindingTargetKind.Talent => "Talent",
            FrontedBindingTargetKind.Trait => "Trait",
            _ => "Any"
        };
    }

    private static IReadOnlyList<string> ResolveAllowedBindingTypeNames(FrontedBindingTargetKind kind)
    {
        return kind switch
        {
            FrontedBindingTargetKind.Text => ["string", "number", "bool", "enum", "DateTime", "TimeSpan"],
            FrontedBindingTargetKind.Image => ["ImageSource", "BitmapSource", "BitmapImage"],
            FrontedBindingTargetKind.GameProgress => ["GameProgress", "GameProgress?"],
            FrontedBindingTargetKind.Map => ["Map", "Map?"],
            FrontedBindingTargetKind.Boolean => ["bool", "bool?"],
            FrontedBindingTargetKind.Number => ["int", "double", "float", "decimal"],
            FrontedBindingTargetKind.String => ["string"],
            FrontedBindingTargetKind.Talent => ["Talent"],
            FrontedBindingTargetKind.Trait => ["Trait"],
            _ => ["Any"]
        };
    }

    private static bool IsSupportedProperty(PropertyInfo property)
    {
        if (property.Name == nameof(FrontedControlConfigBase.BehaviorGuid))
        {
            return false;
        }

        if (property.GetIndexParameters().Length > 0 || !property.CanRead)
        {
            return false;
        }

        if (!property.CanWrite && property.Name != nameof(FrontedControlConfigBase.ControlType))
        {
            return false;
        }

        var type = GetCoreType(property.PropertyType);
        if (type == typeof(FrontedTextBindingExpression))
        {
            return true;
        }

        if (type == typeof(string)
            || type == typeof(bool)
            || type.IsEnum
            || IsNumericType(type))
        {
            return true;
        }

        return typeof(IEnumerable).IsAssignableFrom(type) && type == typeof(string);
    }

    private static FrontedPropertyEditorKind ResolveEditorKind(PropertyInfo property)
    {
        var type = GetCoreType(property.PropertyType);
        if (type == typeof(FrontedTextBindingExpression))
        {
            return FrontedPropertyEditorKind.TextBinding;
        }

        if (property.PropertyType == typeof(string) && IsColorProperty(property.Name))
        {
            return FrontedPropertyEditorKind.Color;
        }

        if (property.PropertyType == typeof(string)
            && IsFontFamilyProperty(property.Name))
        {
            return FrontedPropertyEditorKind.FontFamily;
        }

        if (property.PropertyType == typeof(string) && TryGetStringOptions(property.Name, out _))
        {
            return FrontedPropertyEditorKind.Enum;
        }

        if (type == typeof(bool))
        {
            return FrontedPropertyEditorKind.Boolean;
        }

        if (type.IsEnum)
        {
            return FrontedPropertyEditorKind.Enum;
        }

        if (IsNumericType(type))
        {
            return FrontedPropertyEditorKind.Number;
        }

        return type == typeof(string)
            ? FrontedPropertyEditorKind.Text
            : FrontedPropertyEditorKind.ReadOnly;
    }

    private IReadOnlyList<object>? ResolveOptions(PropertyInfo property, FrontedPropertyEditorKind kind)
    {
        if (kind == FrontedPropertyEditorKind.FontFamily)
        {
            return _fontFamilyOptionProvider.GetFontFamilyOptions().Cast<object>().ToArray();
        }

        if (kind == FrontedPropertyEditorKind.Boolean)
        {
            return
            [
                CreateBooleanOption(true),
                CreateBooleanOption(false)
            ];
        }

        if (kind != FrontedPropertyEditorKind.Enum)
        {
            return null;
        }

        if (property.PropertyType == typeof(string)
            && TryGetStringOptions(property.Name, out var stringOptions))
        {
            return stringOptions
                .Select(value => CreateOption(GetOptionPropertyName(property.Name), value))
                .Cast<object>()
                .ToArray();
        }

        var values = Enum.GetValues(GetCoreType(property.PropertyType))
            .Cast<object>();

        // DisplayLanguage 使用共用的 LanguageKey 枚举，但排除全局的 System 值
        if (property.Name == nameof(GameProgressTextControlConfig.DisplayLanguage))
        {
            values = values.Where(v => v is not LanguageKey.System);
        }

        return values
            .Select(value => CreateOption(property.Name, value))
            .Cast<object>()
            .ToArray();
    }

    private static string ResolveGroupName(string propertyName, FrontedControlConfigBase config)
    {
        if (config is TextFrontedControlConfig
            && propertyName is nameof(TextFrontedControlConfig.Text)
                or nameof(TextFrontedControlConfig.TextBinding)
            || config is LocalizedTextControlConfig
            && propertyName is nameof(LocalizedTextControlConfig.LocalizationKey)
                or nameof(LocalizedTextControlConfig.FallbackText)
                or nameof(LocalizedTextControlConfig.TextBinding))
        {
            return "Content";
        }

        if (config is BorderedImageFrontedControlConfig)
        {
            if (propertyName is nameof(ImageFrontedControlConfig.Lockable)
                or nameof(ImageFrontedControlConfig.LockImagePath)
                or nameof(ImageFrontedControlConfig.LockVisibilityBindingPath)
                or nameof(ImageFrontedControlConfig.LockVisibleWhen)
                or nameof(ImageFrontedControlConfig.LockZIndexOffset)
                or nameof(ImageFrontedControlConfig.PickingBorderAvailable)
                or nameof(ImageFrontedControlConfig.PickingBorderImagePath)
                or nameof(ImageFrontedControlConfig.PickingBorderName)
                or nameof(ImageFrontedControlConfig.PickingBorderZIndexOffset)
                or nameof(ImageFrontedControlConfig.PickingBorder)
                or nameof(ImageFrontedControlConfig.PickingBorderImagePath)
                or nameof(ImageFrontedControlConfig.BanLockAvailable)
                or nameof(ImageFrontedControlConfig.BanLockImagePath))
            {
                return "Overlay";
            }

            if (propertyName is nameof(FrontedControlConfigBase.Left)
                or nameof(FrontedControlConfigBase.Top)
                or nameof(FrontedControlConfigBase.Width)
                or nameof(FrontedControlConfigBase.Height)
                or nameof(FrontedControlConfigBase.ZIndex)
                or nameof(ImageFrontedControlConfig.CornerRadius)
                or nameof(ImageFrontedControlConfig.ClipToBounds))
            {
                return "Border";
            }

            if (propertyName is nameof(FrontedControlConfigBase.BindingPath)
                or nameof(ImageFrontedControlConfig.ImagePath)
                or nameof(BorderedImageFrontedControlConfig.ImageWidth)
                or nameof(BorderedImageFrontedControlConfig.ImageHeight)
                or nameof(ImageFrontedControlConfig.SizingMode)
                or nameof(ImageFrontedControlConfig.Stretch)
                or nameof(ImageFrontedControlConfig.HorizontalAlignment)
                or nameof(ImageFrontedControlConfig.VerticalAlignment))
            {
                return "Image";
            }
        }

        if (propertyName is nameof(FrontedControlConfigBase.Left)
            or nameof(FrontedControlConfigBase.Top)
            or nameof(FrontedControlConfigBase.Width)
            or nameof(FrontedControlConfigBase.Height)
            or nameof(FrontedControlConfigBase.ZIndex))
        {
            return "Layout";
        }

        if (propertyName == nameof(FrontedControlConfigBase.BindingPath)
            || propertyName == nameof(TextFrontedControlConfig.TextBinding))
        {
            return "Binding";
        }

        if (config is ShapeFrontedControlConfigBase)
        {
            // UseGradient is a toggle in the Appearance group, not a binding switch
            if (propertyName == nameof(ShapeFrontedControlConfigBase.UseGradient))
            {
                return "Appearance";
            }

            if (IsBindingPathProperty(propertyName)
                || propertyName.StartsWith("Use", StringComparison.Ordinal))
            {
                return "Binding";
            }

            if (propertyName is nameof(ShapeFrontedControlConfigBase.StrokeColor)
                or nameof(ShapeFrontedControlConfigBase.StrokeThickness))
            {
                return "Border";
            }

            if (propertyName is nameof(ShapeFrontedControlConfigBase.FillColor)
                or nameof(ShapeFrontedControlConfigBase.GradientEndColor)
                or nameof(ShapeFrontedControlConfigBase.GradientAngle))
            {
                return "Appearance";
            }
        }

        if (config is BackgroundTintFrontedControlConfigBase)
        {
            return propertyName == nameof(BackgroundTintFrontedControlConfigBase.TintBindingPath)
                ? "Binding"
                : "Appearance";
        }

        if (config is MapV2DisplayControlConfig
            && propertyName is nameof(MapV2DisplayControlConfig.MapBorderNormalColor)
                or nameof(MapV2DisplayControlConfig.MapBorderBannedColor))
        {
            return "Border";
        }

        if (config is ImageFrontedControlConfig
            && (propertyName is nameof(ImageFrontedControlConfig.Lockable)
                or nameof(ImageFrontedControlConfig.LockImagePath)
                or nameof(ImageFrontedControlConfig.LockVisibilityBindingPath)
                or nameof(ImageFrontedControlConfig.LockVisibleWhen)
                or nameof(ImageFrontedControlConfig.LockZIndexOffset)
                or nameof(ImageFrontedControlConfig.PickingBorderAvailable)
                or nameof(ImageFrontedControlConfig.PickingBorderImagePath)
                or nameof(ImageFrontedControlConfig.PickingBorderName)
                or nameof(ImageFrontedControlConfig.PickingBorderZIndexOffset)))
        {
            return "Overlay";
        }

        if (IsResourcePathProperty(propertyName))
        {
            return "Resource";
        }

        return IsAppearanceProperty(propertyName)
            ? "Appearance"
            : "ControlSpecific";
    }

    private static bool IsVisibleProperty(FrontedControlConfigBase config, string propertyName)
    {
        if (config is TextFrontedControlConfig or LocalizedTextControlConfig
            && propertyName == nameof(FrontedControlConfigBase.BindingPath))
        {
            return false;
        }

        if (config is ShapeFrontedControlConfigBase shapeConfig)
        {
            // Hide the base class BindingPath - shapes use dedicated FillBindingPath etc.
            if (propertyName == nameof(FrontedControlConfigBase.BindingPath))
            {
                return false;
            }

            // Hide FillMode enum - replaced by UseGradient toggle
            if (propertyName == nameof(ShapeFrontedControlConfigBase.FillMode))
            {
                return false;
            }

            // Hide deprecated gradient-start properties (FillColor serves dual purpose)
            if (propertyName is nameof(ShapeFrontedControlConfigBase.GradientStartColor)
                or nameof(ShapeFrontedControlConfigBase.UseGradientStartBinding)
                or nameof(ShapeFrontedControlConfigBase.GradientStartBindingPath))
            {
                return false;
            }

            // Hide binding toggles - binding is automatic when path has a value
            if (propertyName is nameof(ShapeFrontedControlConfigBase.UseFillBinding)
                or nameof(ShapeFrontedControlConfigBase.UseGradientEndBinding))
            {
                return false;
            }

            // When gradient is off, hide gradient-end and angle properties
            var useGradient = shapeConfig.UseGradient || shapeConfig.FillMode == ShapeFillMode.LinearGradient;
            if (!useGradient)
            {
                if (propertyName is nameof(ShapeFrontedControlConfigBase.GradientEndColor)
                    or nameof(ShapeFrontedControlConfigBase.GradientEndBindingPath)
                    or nameof(ShapeFrontedControlConfigBase.GradientAngle))
                {
                    return false;
                }
            }
        }

        if (config is BackgroundTintFrontedControlConfigBase
            && propertyName == nameof(FrontedControlConfigBase.BindingPath))
        {
            return false;
        }

        if (config is ImageFrontedControlConfig and not BorderedImageFrontedControlConfig)
        {
            return propertyName is not nameof(ImageFrontedControlConfig.SizingMode)
                and not nameof(ImageFrontedControlConfig.PickingBorder)
                and not nameof(ImageFrontedControlConfig.BanLockAvailable)
                and not nameof(ImageFrontedControlConfig.BanLockImagePath);
        }

        if (config is BorderedImageFrontedControlConfig)
        {
            return propertyName is not nameof(ImageFrontedControlConfig.PickingBorder)
                and not nameof(ImageFrontedControlConfig.BanLockAvailable)
                and not nameof(ImageFrontedControlConfig.BanLockImagePath);
        }

        return true;
    }

    private static bool IsBindingPathProperty(string propertyName) =>
        propertyName.Equals(nameof(FrontedControlConfigBase.BindingPath), StringComparison.Ordinal)
        || propertyName.EndsWith(nameof(FrontedControlConfigBase.BindingPath), StringComparison.Ordinal);

    private static bool IsResourcePathProperty(string propertyName)
    {
        if (IsBindingPathProperty(propertyName))
        {
            return false;
        }

        return ResourcePathPropertyNames.Contains(propertyName)
               || ResourcePathPropertyNames.Any(propertyName.EndsWith);
    }

    private static bool IsColorProperty(string propertyName) =>
        ColorPropertyNames.Contains(propertyName) || propertyName.EndsWith("Color", StringComparison.OrdinalIgnoreCase);

    private static bool IsFontFamilyProperty(string propertyName) =>
        propertyName.EndsWith("FontFamily", StringComparison.OrdinalIgnoreCase);

    private static bool IsAppearanceProperty(string propertyName) =>
        AppearancePropertyNames.Contains(propertyName)
        || propertyName.EndsWith("Color", StringComparison.OrdinalIgnoreCase)
        || propertyName.EndsWith("FontFamily", StringComparison.OrdinalIgnoreCase)
        || propertyName.EndsWith("FontWeight", StringComparison.OrdinalIgnoreCase)
        || propertyName.EndsWith("FontSize", StringComparison.OrdinalIgnoreCase);

    private static bool TryGetStringOptions(string propertyName, out IReadOnlyList<object> options)
    {
        if (StringOptionProperties.TryGetValue(propertyName, out options!))
        {
            return true;
        }

        if (propertyName.EndsWith("FontWeight", StringComparison.OrdinalIgnoreCase)
            && StringOptionProperties.TryGetValue("FontWeight", out options!))
        {
            return true;
        }

        options = [];
        return false;
    }

    private static string GetOptionPropertyName(string propertyName)
    {
        if (propertyName.EndsWith("FontWeight", StringComparison.OrdinalIgnoreCase))
        {
            return "FontWeight";
        }

        return propertyName;
    }

    private static int GetPropertyOrder(PropertyInfo property)
    {
        if (CommonPropertyNames.Contains(property.Name))
        {
            return property.Name switch
            {
                nameof(FrontedControlConfigBase.Left) => 10,
                nameof(FrontedControlConfigBase.Top) => 11,
                nameof(FrontedControlConfigBase.Width) => 12,
                nameof(FrontedControlConfigBase.Height) => 13,
                nameof(FrontedControlConfigBase.ZIndex) => 14,
                nameof(FrontedControlConfigBase.BindingPath) => 20,
                _ => 30
            };
        }

        return property.DeclaringType == typeof(FrontedControlConfigBase)
            ? 30
            : 100 + property.MetadataToken;
    }

    private IReadOnlyList<string> GetPropertyMessages(
        IEnumerable<FrontedLayoutValidationMessage> messages,
        string controlName,
        string propertyName)
    {
        return GetPropertyValidationMessages(messages, controlName, propertyName)
            .Select(message => message.Message)
            .ToArray();
    }

    private IReadOnlyList<FrontedLayoutValidationMessage> GetPropertyValidationMessages(
        IEnumerable<FrontedLayoutValidationMessage> messages,
        string controlName,
        string propertyName)
    {
        return messages
            .Where(message => message.ControlName == controlName && message.PropertyName == propertyName)
            .Select(LocalizePropertyValidationMessage)
            .ToArray();
    }

    private FrontedLayoutValidationMessage LocalizePropertyValidationMessage(FrontedLayoutValidationMessage message)
    {
        var localizedMessage = (message.Code, message.PropertyName) switch
        {
            ("StaticTextIgnored", nameof(TextFrontedControlConfig.Text)) =>
                _localizationService.GetDesignerText(
                    "Designer.Validation.TextStaticIgnored",
                    "Static Text is ignored while TextBinding has active sources."),
            ("StaticTextIgnored", nameof(LocalizedTextControlConfig.LocalizationKey)) =>
                _localizationService.GetDesignerText(
                    "Designer.Validation.LocalizationKeyIgnored",
                    "LocalizationKey is ignored while TextBinding has active sources."),
            ("ImagePathIgnored", nameof(ImageFrontedControlConfig.ImagePath)) =>
                _localizationService.GetDesignerText(
                    "Designer.Validation.ImagePathIgnored",
                    "ImagePath is ignored while BindingPath is set."),
            ("FillColorIgnored", nameof(ShapeFrontedControlConfigBase.FillColor)) =>
                _localizationService.GetDesignerText(
                    "Designer.Validation.FillColorIgnored",
                    "Static fill color is ignored while binding is active. The bound color value is used instead."),
            ("GradientEndColorIgnored", nameof(ShapeFrontedControlConfigBase.GradientEndColor)) =>
                _localizationService.GetDesignerText(
                    "Designer.Validation.GradientEndColorIgnored",
                    "Static gradient end color is ignored while binding is active. The bound color value is used instead."),
            _ => message.Message
        };

        return new FrontedLayoutValidationMessage
        {
            Severity = message.Severity,
            Code = message.Code,
            Message = localizedMessage,
            ControlName = message.ControlName,
            PropertyName = message.PropertyName
        };
    }

    private static FrontedLayoutValidationMessage CreatePropertyError(
        string message,
        string controlName,
        string propertyName) =>
        new()
        {
            Severity = FrontedLayoutValidationSeverity.Error,
            Code = "PropertyValidationError",
            Message = message,
            ControlName = controlName,
            PropertyName = propertyName
        };

    private void MarkGroupHeaders(IReadOnlyList<FrontedPropertyEditorItem> rows)
    {
        string? currentGroup = null;
        foreach (var row in rows)
        {
            row.IsGroupHeaderVisible = row.GroupName != currentGroup;
            row.GroupDisplayName = string.IsNullOrWhiteSpace(row.GroupName)
                ? row.GroupName
                : _localizationService.GetGroupDisplayName(row.GroupName);
            currentGroup = row.GroupName;
        }
    }

    private FrontedPropertyEditorOption CreateOption(string propertyName, object? value) =>
        new()
        {
            Value = value,
            DisplayName = _localizationService.GetOptionDisplayName(propertyName, value)
        };

    private FrontedPropertyEditorOption CreateBooleanOption(bool value) =>
        new()
        {
            Value = value,
            DisplayName = _localizationService.GetDesignerText(
                value ? "Designer.Value.True" : "Designer.Value.False",
                value ? "true" : "false")
        };

    private string GetDisplayValue(object? value, bool isReadOnly)
    {
        if (value is FrontedTextBindingExpression expression)
        {
            var sources = expression.GetActiveSources();
            return sources.Count == 0
                ? _localizationService.GetDesignerText("Designer.TextBinding.None", "No binding sources")
                : string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    _localizationService.GetDesignerText(
                        "Designer.TextBinding.SourceSummary",
                        "{0} source(s)"),
                    sources.Count,
                    string.Join(", ", sources.Select(s => s.DisplayName ?? s.Path)));
        }

        if (isReadOnly && value is bool boolValue)
        {
            return _localizationService.GetDesignerText(
                boolValue ? "Designer.Value.True" : "Designer.Value.False",
                boolValue ? "True" : "False");
        }

        return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static Type GetCoreType(Type type)
    {
        return Nullable.GetUnderlyingType(type) ?? type;
    }

    private static bool IsNumericType(Type type)
    {
        return type == typeof(byte)
               || type == typeof(short)
               || type == typeof(int)
               || type == typeof(long)
               || type == typeof(float)
               || type == typeof(double)
               || type == typeof(decimal);
    }
}
