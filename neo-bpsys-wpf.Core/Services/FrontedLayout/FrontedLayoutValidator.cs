using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
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

    private readonly IFrontedControlRegistry? _controlRegistry;
    private readonly IFrontedResourceResolver? _resourceResolver;
    private readonly FrontedLayoutRuntimeContractCatalog _runtimeContracts;
    private readonly FrontedLayoutReferenceScanner _referenceScanner;

    /// <summary>
    /// 初始化校验器。
    /// </summary>
    public FrontedLayoutValidator(
        IFrontedControlRegistry? controlRegistry = null,
        IFrontedResourceResolver? resourceResolver = null,
        FrontedLayoutRuntimeContractCatalog? runtimeContracts = null,
        FrontedLayoutReferenceScanner? referenceScanner = null)
    {
        _controlRegistry = controlRegistry;
        _resourceResolver = resourceResolver;
        _runtimeContracts = runtimeContracts ?? new FrontedLayoutRuntimeContractCatalog();
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
        var document = new FrontedLayoutDesignConverter()
            .FromConfig(windowTypeName, canvasName, config, _runtimeContracts);

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
        ValidateRuntimeCriticalNames(document, messages);
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
            item.IsRuntimeCritical = _runtimeContracts.IsRuntimeCritical(
                document.WindowTypeName,
                document.CanvasName,
                item.Name);

            ValidateCommonControlFields(item, messages);
            ValidateKnownControlConfig(item, messages);
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
        else if (_controlRegistry is not null && _controlRegistry.GetControl(item.Config.ControlType) is null)
        {
            messages.Add(Error(
                "ControlTypeUnknown",
                $"Control '{item.Name}' has unknown ControlType '{item.Config.ControlType}'.",
                item.Name,
                nameof(FrontedControlConfigBase.ControlType)));
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

    private static void ValidateKnownControlConfig(
        FrontedControlDesignItem item,
        ICollection<FrontedLayoutValidationMessage> messages)
    {
        switch (item.Config)
        {
            case TextFrontedControlConfig text:
                if (string.IsNullOrWhiteSpace(text.BindingPath) && string.IsNullOrWhiteSpace(text.Text))
                {
                    messages.Add(Warning(
                        "EmptyVisibleContent",
                        $"Text control '{item.Name}' has no BindingPath or static Text.",
                        item.Name,
                        nameof(TextFrontedControlConfig.Text)));
                }

                break;

            case LocalizedTextControlConfig localizedText:
                if (string.IsNullOrWhiteSpace(localizedText.LocalizationKey))
                {
                    messages.Add(Error(
                        "RequiredPropertyMissing",
                        $"LocalizedText control '{item.Name}' requires LocalizationKey.",
                        item.Name,
                        nameof(LocalizedTextControlConfig.LocalizationKey)));
                }

                break;

            case ImageFrontedControlConfig image:
                if (string.IsNullOrWhiteSpace(image.BindingPath))
                {
                    messages.Add(Warning(
                        "EmptyVisibleContent",
                        $"Image control '{item.Name}' has no BindingPath.",
                        item.Name,
                        nameof(ImageFrontedControlConfig.BindingPath)));
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

            case BanSlotDisplayControlConfig banSlot:
                ValidateEnumValue(item.Name, nameof(BanSlotDisplayControlConfig.SlotKind), banSlot.SlotKind, messages);
                ValidateEnumValue(item.Name, nameof(BanSlotDisplayControlConfig.Camp), banSlot.Camp, messages);
                ValidateNonNegativeIndex(item.Name, banSlot.Index, messages);
                break;

            case CurrentBanDisplayControlConfig currentBan:
                ValidateEnumValue(item.Name, nameof(CurrentBanDisplayControlConfig.Camp), currentBan.Camp, messages);
                ValidateNonNegativeIndex(item.Name, currentBan.Index, messages);
                break;

            case MapV2DisplayControlConfig mapV2:
                if (string.IsNullOrWhiteSpace(mapV2.MapKey))
                {
                    messages.Add(Error(
                        "RequiredPropertyMissing",
                        $"MapV2Display control '{item.Name}' requires MapKey.",
                        item.Name,
                        nameof(MapV2DisplayControlConfig.MapKey)));
                }

                break;

            case PickingBorderOverlayControlConfig pickingBorder:
                if (string.IsNullOrWhiteSpace(pickingBorder.TargetControlName))
                {
                    messages.Add(Error(
                        "RequiredPropertyMissing",
                        $"PickingBorderOverlay control '{item.Name}' requires TargetControlName.",
                        item.Name,
                        nameof(PickingBorderOverlayControlConfig.TargetControlName)));
                }

                break;
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

    private void ValidateRuntimeCriticalNames(
        FrontedCanvasDesignDocument document,
        ICollection<FrontedLayoutValidationMessage> messages)
    {
        var expectedNames = _runtimeContracts.GetRuntimeCriticalNames(
            document.WindowTypeName,
            document.CanvasName);

        if (expectedNames.Count == 0)
        {
            return;
        }

        var actualNames = document.Controls
            .Select(control => control.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var expectedName in expectedNames.Where(expectedName => !actualNames.Contains(expectedName)))
        {
            messages.Add(Error(
                "RuntimeCriticalRenameOrDelete",
                $"Runtime-critical control '{expectedName}' is missing.",
                expectedName,
                nameof(FrontedControlDesignItem.Name)));
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
            or TalentTraitDisplayControlConfig
            or CurrentBanDisplayControlConfig
            or BanSlotDisplayControlConfig
            or PickingBorderOverlayControlConfig
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
