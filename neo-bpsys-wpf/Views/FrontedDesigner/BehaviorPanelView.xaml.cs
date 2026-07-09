using System.Windows.Controls;
using System.Windows;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.ViewModels.FrontedDesigner;
using neo_bpsys_wpf.Views.Windows;
using System.Windows.Threading;

namespace neo_bpsys_wpf.Views.FrontedDesigner;

public partial class BehaviorPanelView : UserControl
{
    private FrontedBehaviorAnimationHelpWindow? _helpWindow;

    public BehaviorPanelView()
    {
        InitializeComponent();
        IsVisibleChanged += (_, e) =>
        {
            if (Equals(e.NewValue, true))
            {
                ScheduleTutorialRun();
            }
        };
        DataContextChanged += OnDataContextChanged;
        Unloaded += (_, _) => CloseHelpWindow();
    }

    private void ScheduleTutorialRun()
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(async () =>
            {
                var runner = IAppHost.Host?.Services.GetService(typeof(ITutorialRunner)) as ITutorialRunner;
                if (runner != null)
                {
                    await runner.RunUntilBlockedAsync(this, TutorialPageKey);
                }
            }));
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is BehaviorPanelViewModel oldViewModel)
        {
            oldViewModel.AnimationEditorRequested -= OpenAnimationEditor;
            oldViewModel.CopyBehaviorToRequested -= OpenCopyBehaviorTo;
        }

        if (e.NewValue is BehaviorPanelViewModel newViewModel)
        {
            newViewModel.AnimationEditorRequested += OpenAnimationEditor;
            newViewModel.CopyBehaviorToRequested += OpenCopyBehaviorTo;
            ScheduleTutorialRun();
        }
    }

    private void OpenAnimationEditor(FrontedBehaviorAnimationEditorViewModel viewModel)
    {
        var window = new FrontedBehaviorAnimationEditorWindow(viewModel)
        {
            Owner = System.Windows.Window.GetWindow(this)
        };
        window.ShowDialog();
    }

    private void OpenCopyBehaviorTo(FrontedBehaviorCopyToRequest request)
    {
        var window = new FrontedBehaviorCopyTargetWindow(request)
        {
            Owner = Window.GetWindow(this)
        };
        window.ShowDialog();
    }

    private void BehaviorMoreButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Wpf.Ui.Controls.Button { ContextMenu: { } contextMenu } button)
        {
            return;
        }

        contextMenu.PlacementTarget = button;
        contextMenu.IsOpen = true;
        e.Handled = true;
    }

    private void OpenHelp_OnClick(object sender, RoutedEventArgs e)
    {
        if (_helpWindow is null || !_helpWindow.IsVisible)
        {
            _helpWindow = new FrontedBehaviorAnimationHelpWindow
            {
                Owner = Window.GetWindow(this)
            };
            _helpWindow.Closed += (_, _) => _helpWindow = null;
            _helpWindow.Show();
            return;
        }

        _helpWindow.Activate();
    }

    private void CloseHelpWindow()
    {
        _helpWindow?.Close();
        _helpWindow = null;
    }
}

internal sealed class FrontedBehaviorCopyTargetWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly FrontedBehaviorCopyToRequest _request;
    private readonly Wpf.Ui.Controls.TextBox _searchBox = new();
    private readonly ListBox _targetList = new() { SelectionMode = SelectionMode.Multiple };
    private readonly TextBlock _previewText = new() { TextWrapping = TextWrapping.Wrap };
    private readonly CheckBox _rewriteTargets = new() { IsChecked = true };
    private readonly CheckBox _rewriteIndexes = new() { IsChecked = true };
    private readonly HashSet<FrontedControlDesignItem> _selectedTargets = [];
    private IReadOnlyList<FrontedBehaviorPastePreview> _previews;
    private bool _isRefreshingTargetList;

    public FrontedBehaviorCopyTargetWindow(FrontedBehaviorCopyToRequest request)
    {
        _request = request;
        _previews = request.Previews;
        Title = I18nHelper.GetLocalizedString("Designer.Behaviors.CopyTo");
        Width = 760;
        Height = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _searchBox.PlaceholderText = I18nHelper.GetLocalizedString("Designer.Editor.Search");
        _searchBox.Margin = new Thickness(0, 0, 0, 8);
        _searchBox.TextChanged += (_, _) => RefreshTargetList();
        _targetList.DisplayMemberPath = nameof(FrontedBehaviorCopyTargetItem.Display);
        _targetList.SelectionChanged += (_, _) => OnTargetSelectionChanged();

        _rewriteTargets.Content = I18nHelper.GetLocalizedString("Designer.Behaviors.RewriteTargets");
        _rewriteIndexes.Content = I18nHelper.GetLocalizedString("Designer.Behaviors.RewriteTriggerIndexes");
        _rewriteTargets.Click += (_, _) => RefreshPreviews();
        _rewriteIndexes.Click += (_, _) => RefreshPreviews();

        var confirm = new Wpf.Ui.Controls.Button
        {
            Content = I18nHelper.GetLocalizedString("Confirm"),
            MinWidth = 100
        };
        confirm.Click += (_, _) => PasteSelected();
        var cancel = new Wpf.Ui.Controls.Button
        {
            Content = I18nHelper.GetLocalizedString("Cancel"),
            MinWidth = 100,
            Margin = new Thickness(8, 0, 0, 0)
        };
        cancel.Click += (_, _) => Close();

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { confirm, cancel }
        };
        var options = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { _rewriteTargets, _rewriteIndexes }
        };
        _rewriteIndexes.Margin = new Thickness(16, 0, 0, 0);

        var grid = new Grid { Margin = new Thickness(16) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.Children.Add(new TextBlock
        {
            Text = I18nHelper.GetLocalizedString("Designer.Behaviors.CopyToTargets"),
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });
        Grid.SetRow(_searchBox, 1);
        grid.Children.Add(_searchBox);
        Grid.SetRow(_targetList, 2);
        grid.Children.Add(_targetList);
        Grid.SetRow(options, 3);
        options.Margin = new Thickness(0, 12, 0, 8);
        grid.Children.Add(options);
        Grid.SetRow(_previewText, 4);
        grid.Children.Add(new ScrollViewer { Content = _previewText, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Grid.SetRow(grid.Children[^1], 4);
        Grid.SetRow(buttons, 5);
        buttons.Margin = new Thickness(0, 12, 0, 0);
        grid.Children.Add(buttons);
        Content = grid;
        RefreshTargetList();
        RefreshPreview();
    }

    private FrontedBehaviorPasteOptions CreateOptions() => new()
    {
        RewriteAnimationTargets = _rewriteTargets.IsChecked == true,
        RewriteTriggerIndexes = _rewriteIndexes.IsChecked == true
    };

    private void RefreshPreviews()
    {
        var targets = _request.Previews.Select(preview => preview.Target).ToArray();
        _previews = _request.Panel.PreviewBehaviorTargets(targets, CreateOptions());
        RefreshTargetList();
        RefreshPreview();
    }

    private void RefreshTargetList()
    {
        var searchText = _searchBox.Text?.Trim();
        _isRefreshingTargetList = true;
        try
        {
            _targetList.Items.Clear();
            foreach (var preview in _previews)
            {
                var item = new FrontedBehaviorCopyTargetItem(preview);
                if (!string.IsNullOrEmpty(searchText)
                    && !item.Display.Contains(searchText, StringComparison.CurrentCultureIgnoreCase))
                {
                    continue;
                }

                _targetList.Items.Add(item);
                if (_selectedTargets.Contains(preview.Target))
                {
                    _targetList.SelectedItems.Add(item);
                }
            }
        }
        finally
        {
            _isRefreshingTargetList = false;
        }

        RefreshPreview();
    }

    private void OnTargetSelectionChanged()
    {
        if (_isRefreshingTargetList)
        {
            return;
        }

        foreach (var item in _targetList.Items.OfType<FrontedBehaviorCopyTargetItem>())
        {
            _selectedTargets.Remove(item.Preview.Target);
        }

        foreach (var item in _targetList.SelectedItems.OfType<FrontedBehaviorCopyTargetItem>())
        {
            _selectedTargets.Add(item.Preview.Target);
        }

        RefreshPreview();
    }

    private void RefreshPreview()
    {
        if (_targetList.SelectedItem is not FrontedBehaviorCopyTargetItem item)
        {
            _previewText.Text = I18nHelper.GetLocalizedString("Designer.Behaviors.SelectTargetForPreview");
            return;
        }

        var preview = item.Preview;
        var lines = new List<string>
        {
            $"{I18nHelper.GetLocalizedString("Designer.Behaviors.Target")}: {preview.Target.Name}",
            preview.IsCompatible
                ? I18nHelper.GetLocalizedString("Designer.Behaviors.Compatible")
                : string.Join(Environment.NewLine, preview.CompatibilityErrors)
        };
        lines.AddRange(preview.TargetRewrites.Select(rewrite => $"{rewrite.Before}{Environment.NewLine} -> {rewrite.After}"));
        lines.AddRange(preview.TriggerRewrites.Select(rewrite => $"{rewrite.Before}{Environment.NewLine} -> {rewrite.After}"));
        lines.AddRange(preview.ExternalReferences.Select(reference =>
            $"{I18nHelper.GetLocalizedString("Designer.Behaviors.ExternalReference")}: {reference}"));
        if (!preview.IsTriggerIndexRemapAvailable)
        {
            lines.Add(preview.TriggerIndexRemapUnavailableReason ?? string.Empty);
        }

        _previewText.Text = string.Join(Environment.NewLine + Environment.NewLine, lines);
    }

    private void PasteSelected()
    {
        var targets = _previews
            .Where(preview => preview.IsCompatible && _selectedTargets.Contains(preview.Target))
            .Select(preview => preview.Target)
            .ToArray();
        if (targets.Length == 0)
        {
            return;
        }

        _request.Panel.PasteBehaviorToTargets(targets, CreateOptions());
        DialogResult = true;
        Close();
    }

    private sealed class FrontedBehaviorCopyTargetItem
    {
        public FrontedBehaviorCopyTargetItem(FrontedBehaviorPastePreview preview)
        {
            Preview = preview;
        }

        public FrontedBehaviorPastePreview Preview { get; }

        public string Display => Preview.IsCompatible
            ? $"{Preview.Target.Name}    {I18nHelper.GetLocalizedString("Designer.Behaviors.Compatible")}"
            : $"{Preview.Target.Name}    {string.Join("; ", Preview.CompatibilityErrors)}";
    }
}
