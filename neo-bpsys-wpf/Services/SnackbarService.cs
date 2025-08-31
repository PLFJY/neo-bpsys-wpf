using System.Windows;
using System.Windows.Controls;
using neo_bpsys_wpf.Core.Abstractions.Services;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Services;

public class SnackbarService : ISnackbarService
{
    private SnackbarPresenter? _presenter;
    private Snackbar? _snackbar;

    /// <inheritdoc />
    public TimeSpan DefaultTimeOut { get; set; } = TimeSpan.FromSeconds(5L);

    /// <inheritdoc />
    public void SetSnackbarPresenter(SnackbarPresenter contentPresenter)
    {
        this._presenter = contentPresenter;
    }

    /// <inheritdoc />
    public SnackbarPresenter? GetSnackbarPresenter() => this._presenter;

    /// <inheritdoc />
    public void Show(
        string title,
        DependencyObject message,
        ControlAppearance appearance,
        IconElement? icon,
        TimeSpan timeout,
        bool isCloseButtonEnabled)
    {
        if (this._presenter == null)
            throw new InvalidOperationException("The SnackbarPresenter was never set");
        if (this._snackbar == null)
            this._snackbar = new Snackbar(this._presenter);
        this._snackbar.SetCurrentValue(Snackbar.TitleProperty, (object) title);
        this._snackbar.SetCurrentValue(ContentControl.ContentProperty, (object) message);
        this._snackbar.SetCurrentValue(Snackbar.AppearanceProperty, (object) appearance);
        this._snackbar.SetCurrentValue(Snackbar.IconProperty, (object) icon);
        this._snackbar.SetCurrentValue(Snackbar.TimeoutProperty, (object) (timeout.TotalSeconds == 0.0 ? this.DefaultTimeOut : timeout));
        this._snackbar.SetCurrentValue(Snackbar.IsCloseButtonEnabledProperty, (object) isCloseButtonEnabled);
        this._snackbar.Show(true);
    }
}