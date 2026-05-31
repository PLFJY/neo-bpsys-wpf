using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Events;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Helpers;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace neo_bpsys_wpf.Controls.FrontedLayout;

/// <summary>
/// 内置 v3 本地化静态文本控件工厂。
/// </summary>
public class LocalizedTextFrontedControl(ILogger<LocalizedTextFrontedControl>? logger = null) : IFrontedControl
{
    private readonly ILogger<LocalizedTextFrontedControl>? _logger = logger;

    /// <inheritdoc />
    public string ControlType => "LocalizedText";

    /// <inheritdoc />
    public Type ConfigType => typeof(LocalizedTextControlConfig);

    /// <inheritdoc />
    public FrameworkElement Create(
        string name,
        FrontedControlConfigBase config,
        FrontedControlBuildContext context)
    {
        if (config is not LocalizedTextControlConfig textConfig)
        {
            throw new FrontedLayoutConfigException($"Control '{name}' config is not a LocalizedText config.");
        }

        var settingsHostService = context.Services.GetRequiredService<ISettingsHostService>();
        return new LocalizedTextElement(
            name,
            textConfig,
            settingsHostService,
            _logger ?? context.Logger);
    }

    /// <summary>
    /// 解析本地化文本，资源缺失时使用 fallback 或 key。
    /// </summary>
    public static string ResolveText(string localizationKey, string? fallbackText)
    {
        if (string.IsNullOrWhiteSpace(localizationKey))
        {
            return fallbackText ?? string.Empty;
        }

        var localizedText = I18nHelper.GetLocalizedString(localizationKey);
        return localizedText == localizationKey && fallbackText is not null
            ? fallbackText
            : localizedText;
    }

    private sealed class LocalizedTextElement : Border
    {
        private readonly LocalizedTextControlConfig _config;
        private readonly ISettingsHostService _settingsHostService;
        private readonly TextBlock _textBlock = new();
        private bool _isSubscribed;

        public LocalizedTextElement(
            string name,
            LocalizedTextControlConfig config,
            ISettingsHostService settingsHostService,
            ILogger? logger)
        {
            _config = config;
            _settingsHostService = settingsHostService;

            Name = name;
            Canvas.SetLeft(this, config.Left);
            Canvas.SetTop(this, config.Top);
            Panel.SetZIndex(this, config.ZIndex);

            if (config.Width.HasValue)
            {
                Width = config.Width.Value;
            }

            if (config.Height.HasValue)
            {
                Height = config.Height.Value;
            }

            ApplyTextStyle(_textBlock, config, logger);
            Child = _textBlock;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
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

        private void OnLanguageSettingChanged(object? sender, LanguageChangedEventArgs args) => UpdateText();

        private void UpdateText()
        {
            _textBlock.Text = ResolveText(_config.LocalizationKey, _config.FallbackText);
        }

        private static void ApplyTextStyle(TextBlock textBlock, LocalizedTextControlConfig config, ILogger? logger)
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
            TryApplyTypeConverter<Brush>(
                config.Color,
                value => textBlock.Foreground = value,
                logger,
                nameof(config.Color));

            if (!string.IsNullOrWhiteSpace(config.FontFamily))
            {
                textBlock.FontFamily = config.FontFamily.Contains("pack://application:,,,")
                    ? new FontFamily(
                        new Uri(config.FontFamily[..config.FontFamily.IndexOf('#')]),
                        "./" + config.FontFamily[config.FontFamily.IndexOf('#')..])
                    : new FontFamily(config.FontFamily);
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
