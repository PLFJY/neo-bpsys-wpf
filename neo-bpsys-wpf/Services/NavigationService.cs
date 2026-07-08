// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Microsoft.Extensions.Logging;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;
using neo_bpsys_wpf.Controls.Modern.Navigation;
using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// Provides data for completed main navigation page changes.
/// </summary>
public sealed class NavigationPageChangedEventArgs : EventArgs
{
    /// <summary>Gets the page type that is now displayed.</summary>
    public required Type PageType { get; init; }

    /// <summary>Gets the page content instance that is now displayed.</summary>
    public object? PageContent { get; init; }

    /// <summary>Gets the page change reason.</summary>
    public string Reason { get; init; } = "Navigate";
}

/// <summary>
/// A service that provides methods related to navigation.
/// </summary>
public partial class NavigationService(
    INavigationViewPageProvider pageProvider,
    ISettingsHostService settingsHostService,
    ILogger<NavigationService> logger) : INavigationService
{
    private readonly ILogger<NavigationService> _logger = logger;

    /// <summary>
    /// Gets or sets the control representing navigation.
    /// </summary>
    protected INavigationView? NavigationControl { get; set; }

    /// <summary>Gets the type of the currently displayed main navigation page.</summary>
    public Type? CurrentPageType { get; private set; }

    /// <summary>Gets the currently displayed main navigation page instance.</summary>
    public object? CurrentPageContent { get; private set; }

    /// <summary>Occurs after the main navigation page has changed.</summary>
    public event EventHandler<NavigationPageChangedEventArgs>? PageChanged;

    /// <inheritdoc />
    public INavigationView GetNavigationControl()
    {
        if (IsClassicMode)
        {
            _logger.LogError("NavigationControl is null.");
            throw new ArgumentNullException(nameof(NavigationControl));
        }

        if (NavigationControl is null)
        {
            _logger.LogError("NavigationControl is null.");
            throw new ArgumentNullException(nameof(NavigationControl));
        }

        return NavigationControl;
    }

    /// <inheritdoc />
    public void SetNavigationControl(INavigationView navigation)
    {
        if (IsClassicMode)
        {
            return;
        }

        if (NavigationControl is not null)
        {
            NavigationControl.Navigated -= OnNavigationControlNavigated;
        }

        NavigationControl = navigation;
        NavigationControl.SetPageProviderService(pageProvider);
        NavigationControl.Navigated += OnNavigationControlNavigated;
    }

    /// <inheritdoc />
    public bool Navigate(Type pageType)
    {
        if (IsClassicMode)
        {
            return false;
        }

        ThrowIfNavigationControlIsNull();

        if (IsCurrentPage(pageType))
        {
            return true;
        }

        var navigated = NavigationControl!.Navigate(pageType);
        if (navigated)
        {
            UpdateCurrentPageFromNavigationControl("Navigate");
        }

        return navigated;
    }

    /// <inheritdoc />
    public bool Navigate(Type pageType, object? dataContext)
    {
        if (IsClassicMode)
        {
            return false;
        }

        ThrowIfNavigationControlIsNull();

        if (IsCurrentPage(pageType))
        {
            return true;
        }

        var navigated = NavigationControl!.Navigate(pageType, dataContext);
        if (navigated)
        {
            UpdateCurrentPageFromNavigationControl("Navigate");
        }

        return navigated;
    }

    /// <inheritdoc />
    public bool Navigate(string pageTag)
    {
        if (IsClassicMode)
        {
            return false;
        }

        ThrowIfNavigationControlIsNull();

        var navigated = NavigationControl!.Navigate(pageTag);
        if (navigated)
        {
            UpdateCurrentPageFromNavigationControl("Navigate");
        }

        return navigated;
    }

    /// <inheritdoc />
    public bool Navigate(string pageTag, object? dataContext)
    {
        if (IsClassicMode)
        {
            return false;
        }

        ThrowIfNavigationControlIsNull();

        var navigated = NavigationControl!.Navigate(pageTag, dataContext);
        if (navigated)
        {
            UpdateCurrentPageFromNavigationControl("Navigate");
        }

        return navigated;
    }

    /// <inheritdoc />
    public bool GoBack()
    {
        if (IsClassicMode)
        {
            return false;
        }

        ThrowIfNavigationControlIsNull();

        var navigated = NavigationControl!.GoBack();
        if (navigated)
        {
            UpdateCurrentPageFromNavigationControl("GoBack");
        }

        return navigated;
    }

    /// <inheritdoc />
    public bool NavigateWithHierarchy(Type pageType)
    {
        if (IsClassicMode)
        {
            return false;
        }

        ThrowIfNavigationControlIsNull();

        var navigated = NavigationControl!.NavigateWithHierarchy(pageType);
        if (navigated)
        {
            UpdateCurrentPageFromNavigationControl("NavigateWithHierarchy");
        }

        return navigated;
    }

    /// <inheritdoc />
    public bool NavigateWithHierarchy(Type pageType, object? dataContext)
    {
        if (IsClassicMode)
        {
            return false;
        }

        ThrowIfNavigationControlIsNull();

        var navigated = NavigationControl!.NavigateWithHierarchy(pageType, dataContext);
        if (navigated)
        {
            UpdateCurrentPageFromNavigationControl("NavigateWithHierarchy");
        }

        return navigated;
    }

    protected void ThrowIfNavigationControlIsNull()
    {
        if (NavigationControl is null)
        {
            _logger.LogError("NavigationControl is null.");
            throw new ArgumentNullException(nameof(NavigationControl));
        }
    }

    /// <summary>
    /// Checks whether the specified <paramref name="pageType"/> is the currently displayed page.
    /// </summary>
    private bool IsCurrentPage(Type pageType)
    {
        return CurrentPageType == pageType
            || NavigationControl?.SelectedItem?.TargetPageType == pageType;
    }

    private bool IsClassicMode => settingsHostService.Settings.IsClassicMode;

    private void OnNavigationControlNavigated(NavigationView sender, NavigatedEventArgs args)
    {
        var pageContent = args.Page ?? GetNavigationCurrentContent();
        var pageType = ResolveCurrentPageType(pageContent);
        if (pageType is null)
        {
            return;
        }

        UpdateCurrentPage(pageType, pageContent, "Navigated");
    }

    private void UpdateCurrentPageFromNavigationControl(string reason)
    {
        var pageContent = GetNavigationCurrentContent();
        var pageType = ResolveCurrentPageType(pageContent);
        if (pageType is null)
        {
            return;
        }

        UpdateCurrentPage(pageType, pageContent, reason);
    }

    private object? GetNavigationCurrentContent() =>
        NavigationControl is ModernNavigationView modernNavigation
            ? modernNavigation.CurrentContent
            : null;

    private Type? ResolveCurrentPageType(object? pageContent) =>
        pageContent?.GetType()
        ?? NavigationControl?.SelectedItem?.TargetPageType;

    private void UpdateCurrentPage(Type pageType, object? pageContent, string reason)
    {
        if (CurrentPageType == pageType && ReferenceEquals(CurrentPageContent, pageContent))
        {
            return;
        }

        CurrentPageType = pageType;
        CurrentPageContent = pageContent;
        PageChanged?.Invoke(
            this,
            new NavigationPageChangedEventArgs
            {
                PageType = pageType,
                PageContent = pageContent,
                Reason = reason
            });
    }
}
