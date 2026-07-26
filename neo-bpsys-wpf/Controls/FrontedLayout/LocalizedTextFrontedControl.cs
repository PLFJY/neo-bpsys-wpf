using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Events;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.PluginSdk;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace neo_bpsys_wpf.Controls.FrontedLayout;

/// <summary>
/// 内置 v3 本地化静态文本控件。
/// </summary>
[FrontedV3Control("LocalizedText", IsBuiltIn = true)]
public class LocalizedTextFrontedControl : FrontedV3ControlBase
{
    /// <inheritdoc />
    protected override void OnInitializeFrontedV3(FrontedV3ControlContext context)
    {
        if (context.Config is not LocalizedTextControlConfig textConfig)
        {
            throw new FrontedLayoutConfigException("Control config is not a LocalizedText config.");
        }

        var settingsHostService = context.Services.GetRequiredService<ISettingsHostService>();
        var element = new LocalizedTextElement(
            context.ControlName ?? string.Empty,
            textConfig,
            settingsHostService,
            context.SharedDataService,
            context.Logger);
        Content = element;
    }

    /// <summary>
    /// 解析本地化文本，资源缺失时使用 fallback 或 key。
    /// </summary>
    /// <param name="localizationKey">本地化资源 key。</param>
    /// <param name="fallbackText">资源缺失时使用的 fallback 文本。</param>
    /// <returns>解析后的文本。</returns>
    public static string ResolveText(string localizationKey, string? fallbackText)
    {
        return ResolveText(localizationKey, fallbackText, null);
    }

    /// <summary>
    /// 解析本地化文本，资源缺失时使用 fallback 或 key。
    /// </summary>
    /// <param name="localizationKey">本地化资源 key。</param>
    /// <param name="fallbackText">资源缺失时使用的 fallback 文本。</param>
    /// <param name="culture">目标文化。为 null 时使用当前应用文化。</param>
    /// <returns>解析后的文本。</returns>
    public static string ResolveText(string localizationKey, string? fallbackText, CultureInfo? culture)
    {
        if (string.IsNullOrWhiteSpace(localizationKey))
        {
            return fallbackText ?? string.Empty;
        }

        var localizedText = culture is null
            ? I18nHelper.GetLocalizedStringFromAnyHostDictionary(localizationKey)
            : I18nHelper.GetLocalizedStringFromAnyHostDictionary(localizationKey, culture);
        return localizedText == localizationKey && fallbackText is not null
            ? fallbackText
            : localizedText;
    }

    private sealed class LocalizedTextElement : Border
    {
        private static readonly DependencyProperty RawTextProperty = DependencyProperty.Register(
            nameof(RawText),
            typeof(string),
            typeof(LocalizedTextElement),
            new PropertyMetadata(string.Empty, OnRawTextChanged));

        private readonly LocalizedTextControlConfig _config;
        private readonly ISettingsHostService _settingsHostService;
        private readonly TextBlock _textBlock = new();
        private CultureInfo _currentAppCulture;
        private bool _isSubscribed;

        public LocalizedTextElement(
            string name,
            LocalizedTextControlConfig config,
            ISettingsHostService settingsHostService,
            ISharedDataService sharedDataService,
            ILogger? logger)
        {
            _config = config;
            _settingsHostService = settingsHostService;
            _currentAppCulture = settingsHostService.Settings.CultureInfo;

            Name = name;

            ApplyTextStyle(_textBlock, config, sharedDataService, logger);
            Child = _textBlock;

            if (config.TextBinding?.GetActiveSources().Count > 0)
            {
                BindingOperations.SetBinding(
                    this,
                    RawTextProperty,
                    FrontedTextBindingHelper.CreateMultiBinding(config.TextBinding, sharedDataService));
            }

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private string RawText
        {
            get => (string)GetValue(RawTextProperty);
            set => SetValue(RawTextProperty, value);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_isSubscribed)
            {
                return;
            }

            _isSubscribed = true;
            _settingsHostService.LanguageSettingChanged += OnLanguageSettingChanged;
            UpdateText();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (!_isSubscribed)
            {
                return;
            }

            _isSubscribed = false;
            _settingsHostService.LanguageSettingChanged -= OnLanguageSettingChanged;
        }

        private void OnLanguageSettingChanged(object? sender, LanguageChangedEventArgs args)
        {
            _currentAppCulture = args.CultureInfo;
            UpdateText();
        }

        private void UpdateText()
        {
            _textBlock.Text = _config.TextBinding?.GetActiveSources().Count > 0
                ? ResolveText(RawText, RawText, _currentAppCulture)
                : ResolveText(_config.LocalizationKey, _config.FallbackText, _currentAppCulture);
        }

        private static void OnRawTextChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
        {
            if (dependencyObject is LocalizedTextElement element)
            {
                element.UpdateText();
            }
        }

        private static void ApplyTextStyle(
            TextBlock textBlock,
            LocalizedTextControlConfig config,
            ISharedDataService sharedDataService,
            ILogger? logger)
        {
            TryApplyEnum<HorizontalAlignment>(
                config.HorizontalAlignment,
                value => textBlock.HorizontalAlignment = value,
                logger,
                nameof(config.HorizontalAlignment));
            TryApplyEnum<VerticalAlignment>(
                config.VerticalAlignment,
                value => textBlock.VerticalAlignment = value,
                logger,
                nameof(config.VerticalAlignment));
            TryApplyEnum<TextAlignment>(
                config.TextAlignment,
                value => textBlock.TextAlignment = value,
                logger,
                nameof(config.TextAlignment));
            TryApplyEnum<TextWrapping>(
                config.TextWrapping,
                value => textBlock.TextWrapping = value,
                logger,
                nameof(config.TextWrapping));
            TryApplyTypeConverter<FontWeight>(
                config.FontWeight,
                value => textBlock.FontWeight = value,
                logger,
                nameof(config.FontWeight));
            FrontedTextForegroundBindingHelper.ApplyForeground(
                textBlock,
                config.Color,
                config.ColorBindingPath,
                sharedDataService,
                logger,
                nameof(config.Color));

            if (!string.IsNullOrWhiteSpace(config.FontFamily))
            {
                textBlock.FontFamily = FrontedFontResourceHelper.CreateFontFamily(config.FontFamily, logger: logger);
            }

            if (config.FontSize > 0)
            {
                textBlock.FontSize = config.FontSize;
            }
        }

        private static void TryApplyEnum<T>(
            string? value,
            Action<T> apply,
            ILogger? logger,
            string propertyName)
            where T : struct
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (Enum.TryParse(value, true, out T result))
            {
                apply(result);
                return;
            }

            logger?.LogWarning(
                "Invalid localized fronted control enum value. Property: {PropertyName}, Value: {Value}",
                propertyName,
                value);
        }

        private static void TryApplyTypeConverter<T>(
            string? value,
            Action<T> apply,
            ILogger? logger,
            string propertyName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            try
            {
                var converter = TypeDescriptor.GetConverter(typeof(T));
                if (converter.ConvertFromString(value) is T result)
                {
                    apply(result);
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(
                    ex,
                    "Invalid localized fronted control style value. Property: {PropertyName}, Value: {Value}",
                    propertyName,
                    value);
            }
        }
    }
}
