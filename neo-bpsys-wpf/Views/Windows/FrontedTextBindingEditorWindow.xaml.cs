using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.ViewModels.Windows;
using System.Collections.ObjectModel;
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

        public EditorViewModel(FrontedTextBindingExpression expression)
        {
            Sources = new ObservableCollection<FrontedBindingSourceConfig>(expression.Sources);
            _stringFormat = expression.StringFormat;
            _joinSeparator = expression.JoinSeparator;
            _nullText = expression.NullText;
            _fallbackText = expression.FallbackText;
        }

        public ObservableCollection<FrontedBindingSourceConfig> Sources { get; }

        public string? StringFormat
        {
            get => _stringFormat;
            set => SetProperty(ref _stringFormat, value);
        }

        public string JoinSeparator
        {
            get => _joinSeparator;
            set => SetProperty(ref _joinSeparator, value);
        }

        public string? NullText
        {
            get => _nullText;
            set => SetProperty(ref _nullText, value);
        }

        public string? FallbackText
        {
            get => _fallbackText;
            set => SetProperty(ref _fallbackText, value);
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
