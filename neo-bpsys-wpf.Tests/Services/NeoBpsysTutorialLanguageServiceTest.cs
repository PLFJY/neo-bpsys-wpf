#nullable enable

using System;
using System.Threading.Tasks;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Events;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// Tests neo-bpsys tutorial language integration.
/// </summary>
public sealed class NeoBpsysTutorialLanguageServiceTest
{
    [Fact]
    public async Task DefaultTutorialLanguageServiceReturnsFallbackOptions()
    {
        var service = new NoOpTutorialLanguageService();

        var options = await service.GetLanguageOptionsAsync();

        Assert.Collection(
            options,
            option => Assert.Equal("System", option.Id),
            option => Assert.Equal("zh_Hans", option.Id),
            option => Assert.Equal("en_US", option.Id),
            option => Assert.Equal("ja_JP", option.Id));
    }

    [Fact]
    public async Task NeoBpsysTutorialLanguageServiceReturnsLanguageKeyAlignedOptions()
    {
        var settingsHost = new FakeSettingsHostService();
        settingsHost.Settings.Language = LanguageKey.ja_JP;
        var service = new NeoBpsysTutorialLanguageService(settingsHost);

        var options = await service.GetLanguageOptionsAsync();

        Assert.Collection(
            options,
            option => Assert.Equal("System", option.Id),
            option => Assert.Equal("zh_Hans", option.Id),
            option => Assert.Equal("en_US", option.Id),
            option =>
            {
                Assert.Equal("ja_JP", option.Id);
                Assert.True(option.IsSelected);
            });
    }

    [Fact]
    public async Task ApplyLanguageUsesLanguageOptionId()
    {
        var settingsHost = new FakeSettingsHostService();
        var service = new NeoBpsysTutorialLanguageService(settingsHost);

        await service.ApplyLanguageAsync("en_US");

        Assert.Equal(LanguageKey.en_US, settingsHost.Settings.Language);
        Assert.Equal(1, settingsHost.SaveCount);
    }

    [Fact]
    public void DefaultTutorialAvatarProviderReturnsNoAvatar()
    {
        var provider = new NoOpTutorialAvatarProvider();

        var avatar = provider.GetAvatar(TutorialAvatarPose.Idle);

        Assert.Null(avatar);
    }

    [Theory]
    [InlineData(LanguageKey.zh_Hans, "爱丽丝·德罗斯")]
    [InlineData(LanguageKey.en_US, "Alice DeRoss")]
    [InlineData(LanguageKey.ja_JP, "アリス・デロス")]
    public void AliceTutorialAvatarProviderReturnsLocalizedName(LanguageKey language, string expectedName)
    {
        var settingsHost = new FakeSettingsHostService();
        settingsHost.Settings.Language = language;
        var provider = new AliceTutorialAvatarProvider(settingsHost);

        var avatar = provider.GetAvatar(TutorialAvatarPose.Idle);

        Assert.NotNull(avatar);
        Assert.Equal(expectedName, avatar.DisplayName);
        Assert.NotNull(avatar.ImageSource);
    }

    private sealed class FakeSettingsHostService : ISettingsHostService
    {
        public Settings Settings { get; set; } = new();

        public int SaveCount { get; private set; }

        public event EventHandler<Settings>? SettingsChanged;

        public event EventHandler<LanguageChangedEventArgs>? LanguageSettingChanged;

        public Task SaveConfigAsync()
        {
            SaveCount++;
            return Task.CompletedTask;
        }

        public Task LoadConfig() => Task.CompletedTask;

        public Task ResetConfigAsync() => Task.CompletedTask;

        public Task ResetConfigAsync(FrontedWindowType windowType) => Task.CompletedTask;

        public void RaiseSettingsChanged() => SettingsChanged?.Invoke(this, Settings);

        public void RaiseLanguageSettingChanged(LanguageChangedEventArgs args) => LanguageSettingChanged?.Invoke(this, args);
    }
}
