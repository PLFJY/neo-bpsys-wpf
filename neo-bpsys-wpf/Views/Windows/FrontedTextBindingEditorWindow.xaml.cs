using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.ViewModels.Windows;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Windows;

public partial class FrontedTextBindingEditorWindow : FluentWindow
{
    private readonly FrontedBindingBrowserProvider _bindingBrowserProvider;
    private readonly EditorViewModel _viewModel;

    public FrontedTextBindingEditorWindow(
        FrontedTextBindingExpression? expression,
        FrontedBindingBrowserProvider bindingBrowserProvider)
    {
        InitializeComponent();
        _bindingBrowserProvider = bindingBrowserProvider;
        _viewModel = new EditorViewModel(Clone(expression) ?? new FrontedTextBindingExpression());
        DataContext = _viewModel;
    }

    public FrontedTextBindingExpression? Result { get; private set; }

    private void AddSource_OnClick(object sender, RoutedEventArgs e)
    {
        var path = Browse(null);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        _viewModel.Sources.Add(new FrontedBindingSourceConfig { Path = path });
    }

    private void BrowseSource_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: FrontedBindingSourceConfig source })
        {
            return;
        }

        var path = Browse(source.Path);
        if (!string.IsNullOrWhiteSpace(path))
        {
            source.Path = path;
            SourcesList.Items.Refresh();
        }
    }

    private string? Browse(string? initialPath)
    {
        var browser = new FrontedBindingBrowserWindow
        {
            Owner = this,
            DataContext = new FrontedBindingBrowserWindowViewModel(
                _bindingBrowserProvider,
                FrontedBindingTypeFilter.Text)
        };
        browser.InitializeSelection(initialPath);
        return browser.ShowDialog() == true ? browser.SelectedBindingPath : null;
    }

    private void DeleteSource_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FrontedBindingSourceConfig source })
        {
            _viewModel.Sources.Remove(source);
        }
    }

    private void MoveUp_OnClick(object sender, RoutedEventArgs e) => MoveSource(sender, -1);

    private void MoveDown_OnClick(object sender, RoutedEventArgs e) => MoveSource(sender, 1);

    private void MoveSource(object sender, int offset)
    {
        if (sender is not FrameworkElement { DataContext: FrontedBindingSourceConfig source })
        {
            return;
        }

        var oldIndex = _viewModel.Sources.IndexOf(source);
        var newIndex = oldIndex + offset;
        if (oldIndex < 0 || newIndex < 0 || newIndex >= _viewModel.Sources.Count)
        {
            return;
        }

        _viewModel.Sources.Move(oldIndex, newIndex);
    }

    private void Apply_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Sources.Any(source => string.IsNullOrWhiteSpace(source.Path)))
        {
            _viewModel.ValidationError = I18nHelper.GetLocalizedString("Designer.TextBinding.EmptyPath");
            return;
        }

        if (!FrontedTextBindingHelper.TryValidateStringFormat(
                _viewModel.StringFormat,
                _viewModel.Sources.Count,
                out var error))
        {
            _viewModel.ValidationError = error;
            return;
        }

        Result = _viewModel.CreateExpression();
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private static FrontedTextBindingExpression? Clone(FrontedTextBindingExpression? expression) =>
        expression is null
            ? null
            : JsonSerializer.Deserialize<FrontedTextBindingExpression>(JsonSerializer.Serialize(expression));

    private sealed class EditorViewModel : ObservableObject
    {
        private string? _stringFormat;
        private string _joinSeparator;
        private string? _nullText;
        private string? _fallbackText;
        private string? _validationError;
        private string _previewText;

        public EditorViewModel(FrontedTextBindingExpression expression)
        {
            Sources = new ObservableCollection<FrontedBindingSourceConfig>(expression.Sources);
            _stringFormat = expression.StringFormat;
            _joinSeparator = expression.JoinSeparator;
            _nullText = expression.NullText;
            _fallbackText = expression.FallbackText;
            _previewText = string.Empty;

            Sources.CollectionChanged += OnSourcesCollectionChanged;
            SubscribeSourcePropertyChanges();
            RefreshPreview();
        }

        public ObservableCollection<FrontedBindingSourceConfig> Sources { get; }

        public string? StringFormat
        {
            get => _stringFormat;
            set
            {
                if (SetProperty(ref _stringFormat, value))
                    RefreshPreview();
            }
        }

        public string JoinSeparator
        {
            get => _joinSeparator;
            set
            {
                if (SetProperty(ref _joinSeparator, value))
                    RefreshPreview();
            }
        }

        public string? NullText
        {
            get => _nullText;
            set
            {
                if (SetProperty(ref _nullText, value))
                    RefreshPreview();
            }
        }

        public string? FallbackText
        {
            get => _fallbackText;
            set
            {
                if (SetProperty(ref _fallbackText, value))
                    RefreshPreview();
            }
        }

        public string? ValidationError
        {
            get => _validationError;
            set
            {
                if (SetProperty(ref _validationError, value))
                {
                    OnPropertyChanged(nameof(HasValidationError));
                }
            }
        }

        public bool HasValidationError => !string.IsNullOrEmpty(ValidationError);

        public string PreviewText
        {
            get => _previewText;
            private set => SetProperty(ref _previewText, value);
        }

        private void OnSourcesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems is not null)
            {
                foreach (var item in e.OldItems)
                {
                    if (item is FrontedBindingSourceConfig source)
                        source.PropertyChanged -= OnSourcePropertyChanged;
                }
            }
            if (e.NewItems is not null)
            {
                foreach (var item in e.NewItems)
                {
                    if (item is FrontedBindingSourceConfig source)
                        source.PropertyChanged += OnSourcePropertyChanged;
                }
            }
            RefreshPreview();
        }

        private void OnSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(FrontedBindingSourceConfig.Path) or nameof(FrontedBindingSourceConfig.DisplayName))
                RefreshPreview();
        }

        private void SubscribeSourcePropertyChanges()
        {
            foreach (var source in Sources)
            {
                source.PropertyChanged += OnSourcePropertyChanged;
            }
        }

        private void RefreshPreview()
        {
            var sources = Sources.ToList();
            if (sources.Count == 0)
            {
                PreviewText = I18nHelper.GetLocalizedString("Designer.TextBinding.None");
                return;
            }

            // Build sample placeholder values for preview
            var sampleValues = new string[sources.Count];
            for (int i = 0; i < sources.Count; i++)
            {
                var path = sources[i].Path;
                sampleValues[i] = string.IsNullOrWhiteSpace(path)
                    ? $"[#{i + 1}]"
                    : $"[{System.IO.Path.GetFileName(path) ?? $"#{i + 1}"}]";
            }

            // Try applying StringFormat first
            if (!string.IsNullOrWhiteSpace(StringFormat))
            {
                try
                {
                    PreviewText = string.Format(CultureInfo.InvariantCulture, StringFormat, sampleValues);
                    return;
                }
                catch (FormatException)
                {
                    PreviewText = $"[{I18nHelper.GetLocalizedString("FormatError") ?? "格式无效"}]";
                    return;
                }
            }

            // Without StringFormat, join with separator
            var joined = string.Join(JoinSeparator, sampleValues.Where(v => !string.IsNullOrEmpty(v)));
            PreviewText = string.IsNullOrEmpty(joined) ? "—" : joined;
        }

        public void RefreshPreviewExternal()
        {
            RefreshPreview();
        }

        public FrontedTextBindingExpression CreateExpression() => new()
        {
            Sources = Sources.ToList(),
            StringFormat = StringFormat,
            JoinSeparator = JoinSeparator,
            NullText = NullText,
            FallbackText = FallbackText
        };
    }
}
