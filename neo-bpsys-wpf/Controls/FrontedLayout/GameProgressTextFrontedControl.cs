using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Events;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.PluginSdk;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;
using System.Windows.Shapes;

namespace neo_bpsys_wpf.Controls.FrontedLayout;

/// <summary>
/// 内置 v3 CutScene 对局进度文本业务控件。
/// </summary>
[FrontedV3Control("GameProgressText", IsBuiltIn = true)]
public class GameProgressTextFrontedControl : FrontedV3ControlBase
{
    /// <inheritdoc />
    protected override void OnInitializeFrontedV3(FrontedV3ControlContext context)
    {
        if (context.Config is not GameProgressTextControlConfig textConfig)
        {
            throw new FrontedLayoutConfigException("Control config is not a GameProgressText config.");
        }

        var settingsHostService = context.Services.GetRequiredService<ISettingsHostService>();
        var element = new GameProgressTextElement(
            context.ControlName ?? string.Empty,
            textConfig,
            context.SharedDataService,
            settingsHostService,
            context.Logger);
        Content = element;
    }

    private sealed class GameProgressTextElement : Border
    {
        private readonly GameProgressTextControlConfig _config;
        private readonly ISharedDataService _sharedDataService;
        private readonly ISettingsHostService _settingsHostService;
        private readonly ILogger? _logger;
        private Game? _subscribedGame;
        private CultureInfo _currentAppCulture;
        private bool _isSubscribed;

        public GameProgressTextElement(
            string name,
            GameProgressTextControlConfig config,
            ISharedDataService sharedDataService,
            ISettingsHostService settingsHostService,
            ILogger? logger)
        {
            _config = config;
            _sharedDataService = sharedDataService;
            _settingsHostService = settingsHostService;
            _logger = logger;
            _currentAppCulture = settingsHostService.Settings.CultureInfo;

            Name = name;

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
            _sharedDataService.CurrentGameChanged += OnCurrentGameChanged;
            _sharedDataService.IsBo3ModeChanged += OnIsBo3ModeChanged;
            _settingsHostService.LanguageSettingChanged += OnLanguageSettingChanged;
            SubscribeGame(_sharedDataService.CurrentGame);
            UpdateVisual();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (!_isSubscribed)
            {
                return;
            }

            _isSubscribed = false;
            _sharedDataService.CurrentGameChanged -= OnCurrentGameChanged;
            _sharedDataService.IsBo3ModeChanged -= OnIsBo3ModeChanged;
            _settingsHostService.LanguageSettingChanged -= OnLanguageSettingChanged;
            SubscribeGame(null);
        }

        private void OnCurrentGameChanged(object? sender, EventArgs args)
        {
            SubscribeGame(_sharedDataService.CurrentGame);
            UpdateVisual();
        }

        private void OnIsBo3ModeChanged(object? sender, EventArgs args) => UpdateVisual();

        private void OnLanguageSettingChanged(object? sender, LanguageChangedEventArgs args)
        {
            _currentAppCulture = args.CultureInfo;
            UpdateVisual();
        }

        private void OnCurrentGamePropertyChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (string.IsNullOrEmpty(args.PropertyName) || args.PropertyName == nameof(Game.GameProgress))
            {
                UpdateVisual();
            }
        }

        private void SubscribeGame(Game? game)
        {
            if (_subscribedGame == game)
            {
                return;
            }

            if (_subscribedGame != null)
            {
                _subscribedGame.PropertyChanged -= OnCurrentGamePropertyChanged;
            }

            _subscribedGame = game;

            if (_subscribedGame != null)
            {
                _subscribedGame.PropertyChanged += OnCurrentGamePropertyChanged;
            }
        }

        private void UpdateVisual()
        {
            var progress = _sharedDataService.CurrentGame.GameProgress;
            var isBo3Mode = _sharedDataService.IsBo3Mode;
            var effectiveCulture = GameProgressDisplayHelper.ResolveCulture(_config.DisplayLanguage, _currentAppCulture);

            var parts = GameProgressDisplayHelper.GetParts(
                progress,
                isBo3Mode,
                effectiveCulture,
                _config.NumberStyle);

            var effectiveMode = _config.DisplayMode;

            switch (effectiveMode)
            {
                case GameProgressTextDisplayMode.Inline:
                    BuildInline(parts);
                    break;
                case GameProgressTextDisplayMode.TwoLine:
                    BuildTwoLine(parts);
                    break;
                case GameProgressTextDisplayMode.HorizontalGameOnly:
                    BuildHorizontalGameOnly(parts);
                    break;
                case GameProgressTextDisplayMode.HorizontalHalfOnly:
                    BuildHorizontalHalfOnly(parts);
                    break;
                case GameProgressTextDisplayMode.Vertical:
                    BuildVertical(parts);
                    break;
                case GameProgressTextDisplayMode.VerticalTwoLine:
                    BuildVerticalTwoLine(parts);
                    break;
                case GameProgressTextDisplayMode.VerticalHalfOnly:
                    BuildVerticalHalfOnly(parts);
                    break;
                case GameProgressTextDisplayMode.VerticalGameOnly:
                    BuildVerticalGameOnly(parts);
                    break;
                case GameProgressTextDisplayMode.VerticalGameAndHalf:
                    BuildVerticalTwoLine(parts);
                    break;
                case GameProgressTextDisplayMode.VerticalSeparatedGameAndHalf:
                    BuildVerticalSeparatedGameAndHalf(parts);
                    break;
                case GameProgressTextDisplayMode.RibbonGameOnly:
                    BuildVerticalGameOnly(parts);
                    break;
                default:
                    BuildInline(parts);
                    break;
            }

            ApplyBackgroundAndPadding();
        }

        // ============================================================
        // Inline 模式
        // ============================================================
        private void BuildInline(GameProgressDisplayParts parts)
        {
            var textBlock = CreateStyledTextBlock();
            textBlock.Text = parts.FullText;
            textBlock.TextWrapping = TextWrapping.NoWrap;
            Child = textBlock;
        }

        // ============================================================
        // HorizontalGameOnly 模式
        // ============================================================
        private void BuildHorizontalGameOnly(GameProgressDisplayParts parts)
        {
            if (parts.IsFree)
            {
                BuildInline(parts);
                return;
            }

            BuildSingleLineText(parts.GameText);
        }

        // ============================================================
        // HorizontalHalfOnly 模式
        // ============================================================
        private void BuildHorizontalHalfOnly(GameProgressDisplayParts parts)
        {
            if (parts.IsFree)
            {
                BuildInline(parts);
                return;
            }

            BuildSingleLineText(parts.HalfText);
        }

        // ============================================================
        // Vertical 模式
        // ============================================================
        private void BuildVertical(GameProgressDisplayParts parts)
        {
            BuildVerticalText(parts.FullText);
        }

        // ============================================================
        // TwoLine 模式
        // ============================================================
        private void BuildTwoLine(GameProgressDisplayParts parts)
        {
            if (parts.IsFree)
            {
                BuildInline(parts);
                return;
            }

            var textBlock = CreateStyledTextBlock();
            textBlock.Text = $"{parts.GameText}\n{parts.HalfText}";
            textBlock.TextWrapping = TextWrapping.NoWrap;
            Child = textBlock;
        }

        // ============================================================
        // VerticalHalfOnly 模式
        // ============================================================
        private void BuildVerticalHalfOnly(GameProgressDisplayParts parts)
        {
            if (parts.IsFree)
            {
                BuildVertical(parts);
                return;
            }

            var verticalMode = ResolveVerticalLanguageMode();
            var text = parts.HalfText;

            if (verticalMode == GameProgressVerticalLanguageMode.RotateBlock)
            {
                Child = BuildRotatedTextBlock(text);
            }
            else if (verticalMode == GameProgressVerticalLanguageMode.StackCharacters)
            {
                Child = BuildStackedCharacters(text);
            }
            else
            {
                Child = BuildUprightVertical(text);
            }
        }

        // ============================================================
        // VerticalGameOnly 模式
        // ============================================================
        private void BuildVerticalGameOnly(GameProgressDisplayParts parts)
        {
            if (parts.IsFree)
            {
                BuildVertical(parts);
                return;
            }

            var verticalMode = ResolveVerticalLanguageMode();
            var text = parts.GameText;

            if (verticalMode == GameProgressVerticalLanguageMode.RotateBlock)
            {
                Child = BuildRotatedTextBlock(text);
            }
            else if (verticalMode == GameProgressVerticalLanguageMode.StackCharacters)
            {
                Child = BuildStackedCharacters(text);
            }
            else
            {
                Child = BuildUprightVertical(text);
            }
        }

        // ============================================================
        // VerticalTwoLine 模式
        // ============================================================
        private void BuildVerticalTwoLine(GameProgressDisplayParts parts)
        {
            if (parts.IsFree)
            {
                BuildVertical(parts);
                return;
            }

            BuildVerticalGameAndHalf(parts);
        }

        // ============================================================
        // VerticalGameAndHalf 模式
        // ============================================================
        private void BuildVerticalGameAndHalf(GameProgressDisplayParts parts)
        {
            if (parts.IsFree)
            {
                BuildVertical(parts);
                return;
            }

            var verticalMode = ResolveVerticalLanguageMode();
            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = ResolveVerticalAlignment()
            };

            UIElement gameElement;
            UIElement halfElement;

            if (verticalMode == GameProgressVerticalLanguageMode.RotateBlock)
            {
                gameElement = BuildRotatedTextBlock(parts.GameText);
                halfElement = BuildRotatedTextBlock(parts.HalfText);
            }
            else if (verticalMode == GameProgressVerticalLanguageMode.StackCharacters)
            {
                gameElement = BuildStackedCharacters(parts.GameText);
                halfElement = BuildStackedCharacters(parts.HalfText);
            }
            else
            {
                gameElement = BuildUprightVertical(parts.GameText);
                halfElement = BuildUprightVertical(parts.HalfText);
            }

            stackPanel.Children.Add(gameElement);
            stackPanel.Children.Add(halfElement);

            if (_config.GroupSpacing > 0)
            {
                stackPanel.Margin = new Thickness(0, 0, 0, 0);
                foreach (var child in stackPanel.Children)
                {
                    if (child is FrameworkElement fe && fe != gameElement)
                    {
                        fe.Margin = new Thickness(0, _config.GroupSpacing, 0, 0);
                    }
                }
            }

            Child = stackPanel;
        }

        // ============================================================
        // VerticalSeparatedGameAndHalf 模式
        // ============================================================
        private void BuildVerticalSeparatedGameAndHalf(GameProgressDisplayParts parts)
        {
            if (parts.IsFree)
            {
                BuildVertical(parts);
                return;
            }

            var verticalMode = ResolveVerticalLanguageMode();
            var grid = new Grid();

            // Define rows: Game | Separator | Half
            var rowGame = new RowDefinition { Height = GridLength.Auto };
            var rowSeparator = _config.ShowSeparator
                ? new RowDefinition { Height = GridLength.Auto }
                : new RowDefinition { Height = new GridLength(_config.GroupSpacing) };
            var rowHalf = new RowDefinition { Height = GridLength.Auto };
            grid.RowDefinitions.Add(rowGame);
            grid.RowDefinitions.Add(rowSeparator);
            grid.RowDefinitions.Add(rowHalf);

            UIElement gameElement;
            UIElement halfElement;

            if (verticalMode == GameProgressVerticalLanguageMode.RotateBlock)
            {
                gameElement = BuildRotatedTextBlock(parts.GameText);
                halfElement = BuildRotatedTextBlock(parts.HalfText);
            }
            else if (verticalMode == GameProgressVerticalLanguageMode.StackCharacters)
            {
                gameElement = BuildStackedCharacters(parts.GameText);
                halfElement = BuildStackedCharacters(parts.HalfText);
            }
            else
            {
                gameElement = BuildUprightVertical(parts.GameText);
                halfElement = BuildUprightVertical(parts.HalfText);
            }

            Grid.SetRow((UIElement)gameElement, 0);
            grid.Children.Add(gameElement);

            if (_config.ShowSeparator)
            {
                var separator = new Rectangle
                {
                    Height = _config.SeparatorThickness,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, _config.GroupSpacing / 2, 0, _config.GroupSpacing / 2)
                };

                if (!string.IsNullOrWhiteSpace(_config.SeparatorColor))
                {
                    TryApplyBrush(_config.SeparatorColor, brush => separator.Fill = brush);
                }
                else
                {
                    separator.Fill = TryCreateBrush("#FFFFFFFF");
                }

                Grid.SetRow(separator, 1);
                grid.Children.Add(separator);
            }

            Grid.SetRow((UIElement)halfElement, 2);
            grid.Children.Add(halfElement);

            grid.HorizontalAlignment = HorizontalAlignment.Center;
            grid.VerticalAlignment = ResolveVerticalAlignment();

            Child = grid;
        }

        // ============================================================
        // RibbonGameOnly 模式
        // ============================================================
        private void BuildRibbonGameOnly(GameProgressDisplayParts parts)
        {
            if (parts.IsFree)
            {
                BuildInline(parts);
                return;
            }

            var verticalMode = ResolveVerticalLanguageMode();
            var text = parts.GameText;

            if (verticalMode == GameProgressVerticalLanguageMode.Upright
                || verticalMode == GameProgressVerticalLanguageMode.StackCharacters)
            {
                Child = verticalMode == GameProgressVerticalLanguageMode.StackCharacters
                    ? BuildStackedCharacters(text)
                    : BuildUprightVertical(text);
            }
            else
            {
                Child = BuildRotatedTextBlock(text);
            }
        }

        // ============================================================
        // 竖向构建辅助方法
        // ============================================================

        private void BuildSingleLineText(string text)
        {
            var textBlock = CreateStyledTextBlock();
            textBlock.Text = text;
            textBlock.TextWrapping = TextWrapping.NoWrap;
            Child = textBlock;
        }

        private void BuildVerticalText(string text)
        {
            var verticalMode = ResolveVerticalLanguageMode();

            if (verticalMode == GameProgressVerticalLanguageMode.RotateBlock)
            {
                Child = BuildRotatedTextBlock(text);
            }
            else if (verticalMode == GameProgressVerticalLanguageMode.StackCharacters)
            {
                Child = BuildStackedCharacters(text);
            }
            else
            {
                Child = BuildUprightVertical(text);
            }
        }

        /// <summary>
        /// 构建逐字竖向排列（CJK upright）。
        /// 使用 StringInfo.GetTextElementEnumerator 按 text element 切分，
        /// 避免 emoji / surrogate pair / 组合字符问题。
        /// </summary>
        private StackPanel BuildUprightVertical(string text)
        {
            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(text);
            var elements = new System.Collections.Generic.List<string>();
            while (enumerator.MoveNext())
            {
                elements.Add(enumerator.GetTextElement());
            }

            for (int i = 0; i < elements.Count; i++)
            {
                var tb = CreateStyledTextBlock();
                tb.Text = elements[i];
                tb.TextWrapping = TextWrapping.NoWrap;
                tb.HorizontalAlignment = HorizontalAlignment.Center;

                if (_config.VerticalTextSpacing > 0 && i < elements.Count - 1)
                {
                    tb.Margin = new Thickness(0, 0, 0, _config.VerticalTextSpacing);
                }

                stackPanel.Children.Add(tb);
            }

            return stackPanel;
        }

        /// <summary>
        /// 构建逐字符纵向堆叠（英文 StackCharacters）。
        /// </summary>
        private StackPanel BuildStackedCharacters(string text)
        {
            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            for (int i = 0; i < text.Length; i++)
            {
                var tb = CreateStyledTextBlock();
                tb.Text = text[i].ToString();
                tb.TextWrapping = TextWrapping.NoWrap;
                tb.HorizontalAlignment = HorizontalAlignment.Center;

                if (_config.VerticalTextSpacing > 0 && i < text.Length - 1)
                {
                    tb.Margin = new Thickness(0, 0, 0, _config.VerticalTextSpacing);
                }

                stackPanel.Children.Add(tb);
            }

            return stackPanel;
        }

        /// <summary>
        /// 构建旋转的文本块（英文 RotateBlock）。
        /// 朝左时逆时针旋转 90°（文本向上），朝右时顺时针旋转 90°（文本向下）。
        /// </summary>
        private TextBlock BuildRotatedTextBlock(string text)
        {
            var direction = ResolveVerticalDirection();

            var textBlock = CreateStyledTextBlock();
            textBlock.Text = text;
            textBlock.TextWrapping = TextWrapping.NoWrap;
            textBlock.HorizontalAlignment = HorizontalAlignment.Center;
            textBlock.VerticalAlignment = VerticalAlignment.Center;

            textBlock.LayoutTransform = new RotateTransform(
                direction == GameProgressVerticalDirection.FacingRight ? 90 : -90);

            return textBlock;
        }

        // ============================================================
        // 通用样式辅助方法
        // ============================================================

        private TextBlock CreateStyledTextBlock()
        {
            var textBlock = new TextBlock();
            CutSceneFrontedControlHelper.ApplyTextStyle(
                textBlock,
                _config.HorizontalAlignment,
                _config.VerticalAlignment,
                _config.TextAlignment,
                _config.FontFamily,
                _config.FontWeight,
                _config.Color,
                _config.ColorBindingPath,
                _config.FontSize,
                _sharedDataService,
                _logger);

            return textBlock;
        }

        private void ApplyBackgroundAndPadding()
        {
            if (!string.IsNullOrWhiteSpace(_config.BackgroundColor))
            {
                if (ColorHelper.TryNormalizeHex(_config.BackgroundColor, out var normalized))
                {
                    Background = TryCreateBrush(normalized);
                }
                else
                {
                    TryApplyBrush(_config.BackgroundColor, brush => Background = brush);
                }
            }

            var hasPadding = _config.PaddingLeft > 0
                             || _config.PaddingTop > 0
                             || _config.PaddingRight > 0
                             || _config.PaddingBottom > 0;
            if (hasPadding)
            {
                Padding = new Thickness(
                    _config.PaddingLeft,
                    _config.PaddingTop,
                    _config.PaddingRight,
                    _config.PaddingBottom);
            }
        }

        private GameProgressVerticalLanguageMode ResolveVerticalLanguageMode()
        {
            if (_config.VerticalLanguageMode != GameProgressVerticalLanguageMode.Auto)
            {
                return _config.VerticalLanguageMode;
            }

            var culture = GameProgressDisplayHelper.ResolveCulture(_config.DisplayLanguage, _currentAppCulture);
            var isCjk = GameProgressDisplayHelper.IsCjkCulture(culture);

            return isCjk
                ? GameProgressVerticalLanguageMode.Upright
                : _config.LatinVerticalMode switch
                {
                    GameProgressLatinVerticalMode.StackCharacters => GameProgressVerticalLanguageMode.StackCharacters,
                    _ => GameProgressVerticalLanguageMode.RotateBlock
                };
        }

        private GameProgressVerticalDirection ResolveVerticalDirection()
        {
            if (_config.VerticalDirection != GameProgressVerticalDirection.Auto)
            {
                return _config.VerticalDirection;
            }

            // Auto: both CJK and non-CJK default to FacingLeft (current behavior)
            return GameProgressVerticalDirection.FacingLeft;
        }

        private static VerticalAlignment ResolveVerticalAlignment()
        {
            return VerticalAlignment.Center;
        }

        private static Brush? TryCreateBrush(string? colorHex)
        {
            if (string.IsNullOrWhiteSpace(colorHex))
            {
                return null;
            }

            if (ColorHelper.TryNormalizeHex(colorHex, out var normalized))
            {
                try
                {
                    var converter = System.ComponentModel.TypeDescriptor.GetConverter(typeof(Brush));
                    return converter.ConvertFromString(normalized) as Brush;
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        private static void TryApplyBrush(string color, Action<Brush> apply)
        {
            if (ColorHelper.TryNormalizeHex(color, out var normalized))
            {
                try
                {
                    var converter = System.ComponentModel.TypeDescriptor.GetConverter(typeof(Brush));
                    if (converter.ConvertFromString(normalized) is Brush brush)
                    {
                        apply(brush);
                    }
                }
                catch
                {
                    // Ignore invalid color
                }
            }
        }
    }
}
