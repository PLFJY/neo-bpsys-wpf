using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace neo_bpsys_wpf.Controls.FrontedLayout;

/// <summary>
/// 内置 v3 CutScene 天赋/辅助特质业务控件工厂。
/// </summary>
public class TalentTraitDisplayFrontedControl(ILogger<TalentTraitDisplayFrontedControl>? logger = null) : IFrontedControl
{
    private readonly ILogger<TalentTraitDisplayFrontedControl>? _logger = logger;

    /// <inheritdoc />
    public string ControlType => "TalentTraitDisplay";

    /// <inheritdoc />
    public Type ConfigType => typeof(TalentTraitDisplayControlConfig);

    /// <inheritdoc />
    public FrameworkElement Create(
        string name,
        FrontedControlConfigBase config,
        FrontedControlBuildContext context)
    {
        if (config is not TalentTraitDisplayControlConfig talentConfig)
        {
            throw new FrontedLayoutConfigException($"Control '{name}' config is not a TalentTraitDisplay config.");
        }

        var settingsHostService = context.Services.GetRequiredService<ISettingsHostService>();
        return new TalentTraitDisplayElement(
            name,
            talentConfig,
            context.SharedDataService,
            settingsHostService,
            _logger ?? context.Logger);
    }

    private sealed class TalentTraitDisplayElement : Border
    {
        private static readonly TalentIconDefinition[] SurvivorTalentDefinitions =
        [
            new(Camp.Sur, nameof(Talent.BorrowedTime), talent => talent.BorrowedTime),
            new(Camp.Sur, nameof(Talent.TideTurner), talent => talent.TideTurner),
            new(Camp.Sur, nameof(Talent.FlywheelEffect), talent => talent.FlywheelEffect),
            new(Camp.Sur, nameof(Talent.KneeJerkReflex), talent => talent.KneeJerkReflex)
        ];

        private static readonly TalentIconDefinition[] HunterTalentDefinitions =
        [
            new(Camp.Hun, nameof(Talent.TrumpCard), talent => talent.TrumpCard),
            new(Camp.Hun, nameof(Talent.Detention), talent => talent.Detention),
            new(Camp.Hun, nameof(Talent.ConfinedSpace), talent => talent.ConfinedSpace),
            new(Camp.Hun, nameof(Talent.Insolence), talent => talent.Insolence)
        ];

        private readonly TalentTraitDisplayControlConfig _config;
        private readonly ISharedDataService _sharedDataService;
        private readonly ILogger? _logger;
        private Player? _subscribedPlayer;
        private Talent? _subscribedTalent;
        private bool _isSubscribed;

        public TalentTraitDisplayElement(
            string name,
            TalentTraitDisplayControlConfig config,
            ISharedDataService sharedDataService,
            ISettingsHostService settingsHostService,
            ILogger? logger)
        {
            Name = name;
            _config = config;
            _sharedDataService = sharedDataService;
            _logger = logger;

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
            _sharedDataService.IsTraitVisibleChanged += OnTraitVisibilityChanged;
            SubscribePlayer(ResolvePlayer());
            Render();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (!_isSubscribed)
            {
                return;
            }

            _isSubscribed = false;
            _sharedDataService.CurrentGameChanged -= OnCurrentGameChanged;
            _sharedDataService.IsTraitVisibleChanged -= OnTraitVisibilityChanged;
            SubscribePlayer(null);
        }

        private void OnCurrentGameChanged(object? sender, EventArgs args)
        {
            SubscribePlayer(ResolvePlayer());
            Render();
        }

        private void OnTraitVisibilityChanged(object? sender, EventArgs args) => Render();

        private void OnPlayerPropertyChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (string.IsNullOrEmpty(args.PropertyName)
                || args.PropertyName == nameof(Player.Talent)
                || args.PropertyName == nameof(Player.Trait))
            {
                SubscribeTalent(_subscribedPlayer?.Talent);
                Render();
            }
        }

        private void OnTalentPropertyChanged(object? sender, PropertyChangedEventArgs args) => Render();

        private void SubscribePlayer(Player? player)
        {
            if (_subscribedPlayer == player)
            {
                return;
            }

            if (_subscribedPlayer != null)
            {
                _subscribedPlayer.PropertyChanged -= OnPlayerPropertyChanged;
            }

            _subscribedPlayer = player;

            if (_subscribedPlayer != null)
            {
                _subscribedPlayer.PropertyChanged += OnPlayerPropertyChanged;
            }

            SubscribeTalent(_subscribedPlayer?.Talent);
        }

        private void SubscribeTalent(Talent? talent)
        {
            if (_subscribedTalent == talent)
            {
                return;
            }

            if (_subscribedTalent != null)
            {
                _subscribedTalent.PropertyChanged -= OnTalentPropertyChanged;
            }

            _subscribedTalent = talent;

            if (_subscribedTalent != null)
            {
                _subscribedTalent.PropertyChanged += OnTalentPropertyChanged;
            }
        }

        private Player? ResolvePlayer()
        {
            return _config.DisplayKind switch
            {
                TalentTraitDisplayKind.SurvivorTalent => ResolveSurvivorPlayer(),
                TalentTraitDisplayKind.HunterTalent or TalentTraitDisplayKind.HunterTrait =>
                    _sharedDataService.CurrentGame?.HunPlayer,
                _ => null
            };
        }

        private Player? ResolveSurvivorPlayer()
        {
            if (!_config.HasValidSurvivorPlayerIndex())
            {
                _logger?.LogWarning(
                    "Invalid SurvivorTalent PlayerIndex for CutScene TalentTraitDisplay. Name: {Name}, PlayerIndex: {PlayerIndex}",
                    Name,
                    _config.PlayerIndex);
                return null;
            }

            var playerIndex = _config.PlayerIndex!.Value;
            var playerList = _sharedDataService.CurrentGame?.SurPlayerList;
            return playerList is not null && playerIndex < playerList.Count
                ? playerList[playerIndex]
                : null;
        }

        private void Render()
        {
            Child = _config.DisplayKind == TalentTraitDisplayKind.HunterTrait
                ? CreateTraitImage()
                : CreateTalentPanel();
        }

        private StackPanel CreateTalentPanel()
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            CutSceneFrontedControlHelper.TryApplyEnum<HorizontalAlignment>(
                _config.HorizontalAlignment,
                value => panel.HorizontalAlignment = value,
                _logger,
                nameof(_config.HorizontalAlignment));
            CutSceneFrontedControlHelper.TryApplyEnum<VerticalAlignment>(
                _config.VerticalAlignment,
                value => panel.VerticalAlignment = value,
                _logger,
                nameof(_config.VerticalAlignment));

            var talent = _subscribedPlayer?.Talent;
            if (talent is null)
            {
                return panel;
            }

            var definitions = _config.DisplayKind == TalentTraitDisplayKind.SurvivorTalent
                ? SurvivorTalentDefinitions
                : HunterTalentDefinitions;

            var visibleIconIndex = 0;
            foreach (var definition in definitions)
            {
                if (!definition.IsVisible(talent))
                {
                    continue;
                }

                var image = CreateIconImage(
                    ImageHelper.GetTalentImageSource(
                        definition.Camp,
                        definition.TalentName,
                        false),
                    visibleIconIndex);
                panel.Children.Add(image);
                visibleIconIndex++;
            }

            return panel;
        }

        private Image CreateTraitImage()
        {
            var image = CreateIconImage(null, visibleIconIndex: 0);
            if (_config.RespectTraitVisibility && !_sharedDataService.IsTraitVisible)
            {
                image.Visibility = Visibility.Collapsed;
                return image;
            }

            image.Source = ImageHelper.GetTraitImageSource(
                _subscribedPlayer?.Trait.TraitName,
                false);
            return image;
        }

        private Image CreateIconImage(ImageSource? source, int visibleIconIndex)
        {
            var image = new Image
            {
                Source = source,
                Stretch = Stretch.UniformToFill
            };

            var iconSize = _config.IconSize;
            if (iconSize > 0)
            {
                image.Width = iconSize;
                image.Height = iconSize;
            }
            else
            {
                if (_config.Width.HasValue)
                {
                    image.Width = _config.Width.Value;
                }

                if (_config.Height.HasValue)
                {
                    image.Height = _config.Height.Value;
                }
            }

            if (visibleIconIndex > 0 && _config.IconGap > 0)
            {
                image.Margin = new Thickness(_config.IconGap, 0, 0, 0);
            }

            return image;
        }
    }

    private sealed record TalentIconDefinition(Camp Camp, string TalentName, Func<Talent, bool> IsVisible);
}
