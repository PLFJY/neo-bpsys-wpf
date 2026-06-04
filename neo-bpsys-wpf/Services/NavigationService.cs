// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Microsoft.Extensions.Logging;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;
using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.Services;

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

        NavigationControl = navigation;
        NavigationControl.SetPageProviderService(pageProvider);
    }

    /// <inheritdoc />
    public bool Navigate(Type pageType)
    {
        if (IsClassicMode)
        {
            return false;
        }

        ThrowIfNavigationControlIsNull();

        return NavigationControl!.Navigate(pageType);
    }

    /// <inheritdoc />
    public bool Navigate(Type pageType, object? dataContext)
    {
        if (IsClassicMode)
        {
            return false;
        }

        ThrowIfNavigationControlIsNull();

        return NavigationControl!.Navigate(pageType, dataContext);
    }

    /// <inheritdoc />
    public bool Navigate(string pageTag)
    {
        if (IsClassicMode)
        {
            return false;
        }

        ThrowIfNavigationControlIsNull();

        return NavigationControl!.Navigate(pageTag);
    }

    /// <inheritdoc />
    public bool Navigate(string pageTag, object? dataContext)
    {
        if (IsClassicMode)
        {
            return false;
        }

        ThrowIfNavigationControlIsNull();

        return NavigationControl!.Navigate(pageTag, dataContext);
    }

    /// <inheritdoc />
    public bool GoBack()
    {
        if (IsClassicMode)
        {
            return false;
        }

        ThrowIfNavigationControlIsNull();

        return NavigationControl!.GoBack();
    }

    /// <inheritdoc />
    public bool NavigateWithHierarchy(Type pageType)
    {
        if (IsClassicMode)
        {
            return false;
        }

        ThrowIfNavigationControlIsNull();

        return NavigationControl!.NavigateWithHierarchy(pageType);
    }

    /// <inheritdoc />
    public bool NavigateWithHierarchy(Type pageType, object? dataContext)
    {
        if (IsClassicMode)
        {
            return false;
        }

        ThrowIfNavigationControlIsNull();

        return NavigationControl!.NavigateWithHierarchy(pageType, dataContext);
    }

    protected void ThrowIfNavigationControlIsNull()
    {
        if (NavigationControl is null)
        {
            _logger.LogError("NavigationControl is null.");
            throw new ArgumentNullException(nameof(NavigationControl));
        }
    }

    private bool IsClassicMode => settingsHostService.Settings.IsClassicMode;
}
