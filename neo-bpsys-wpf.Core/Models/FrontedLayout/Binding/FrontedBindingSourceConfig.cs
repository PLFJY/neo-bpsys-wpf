using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;

/// <summary>
/// One ordered source in a text binding expression.
/// </summary>
public sealed class FrontedBindingSourceConfig : INotifyPropertyChanged
{
    private string _path = string.Empty;

    /// <summary>
    /// Binding path relative to <see cref="Abstractions.Services.ISharedDataService"/>.
    /// </summary>
    public string Path
    {
        get => _path;
        set
        {
            if (_path != value)
            {
                _path = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Optional designer-only display name.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Reserved per-source format.
    /// </summary>
    public string? Format { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
