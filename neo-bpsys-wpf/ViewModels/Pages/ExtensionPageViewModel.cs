using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Plugins.Services;
using neo_bpsys_wpf.Plugins.Hosting;
using neo_bpsys_wpf.Core.Plugins.Abstractions;
using neo_bpsys_wpf.Core.Plugins.Services;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class ExtensionPageViewModel : ViewModelBase
{
	public ExtensionPageViewModel()
	{
		// Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
	}

	private readonly IPluginManager _pluginManager;
	private readonly PluginSystemOptions _options;

	public ExtensionPageViewModel(IPluginManager pluginManager, PluginSystemOptions options)
	{
		_pluginManager = pluginManager;
		_options = options;

		PluginsDirectory = _options.PluginsDirectory;

		_ = Task.Run(async () =>
		{
			try
			{
				await RefreshAsync();
			}
			catch
			{
				// ignore background refresh errors
			}
		});
	}

	[ObservableProperty]
	private string _pluginsDirectory = string.Empty;

	public ObservableCollection<PluginListItem> Plugins { get; } = new();

	[RelayCommand]
	private async Task RefreshAsync()
	{
		if (_pluginManager is neo_bpsys_wpf.Plugins.Services.PluginManager manager)
		{
			await manager.LoadAllPluginsAsync();
		}

		var items = _pluginManager.LoadedPlugins
			.OrderBy(p => p.Metadata.Name)
			.Select(p => new PluginListItem(
				id: p.Metadata.Id,
				name: p.Metadata.Name,
				version: p.Metadata.Version?.ToString() ?? string.Empty,
				state: _pluginManager.GetPluginState(p.Metadata.Id)))
			.ToList();

		await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
		{
			Plugins.Clear();
			foreach (var item in items)
			{
				Plugins.Add(item);
			}
		});
	}

	[RelayCommand]
	private void OpenPluginsDirectory()
	{
		var dir = PluginsDirectory;
		if (string.IsNullOrWhiteSpace(dir))
			return;

		Process.Start(new ProcessStartInfo
		{
			FileName = dir,
			UseShellExecute = true
		});
	}

	[RelayCommand]
	private async Task EnablePlugin(PluginListItem? item)
	{
		if (item == null)
			return;

		await _pluginManager.EnablePluginAsync(item.Id);
		await RefreshAsync();
	}

	[RelayCommand]
	private async Task DisablePlugin(PluginListItem? item)
	{
		if (item == null)
			return;

		await _pluginManager.DisablePluginAsync(item.Id);
		await RefreshAsync();
	}

	[RelayCommand]
	private async Task ReloadPlugin(PluginListItem? item)
	{
		if (item == null)
			return;

		await _pluginManager.ReloadPluginAsync(item.Id);
		await RefreshAsync();
	}

	[RelayCommand]
	private async Task UnloadPlugin(PluginListItem? item)
	{
		if (item == null)
			return;

		await _pluginManager.UnloadPluginAsync(item.Id);
		await RefreshAsync();
	}
}

public partial class PluginListItem : ObservableObject
{
	public PluginListItem(string id, string name, string version, PluginState state)
	{
		Id = id;
		Name = name;
		Version = version;
		State = state;
	}

	public string Id { get; }
	public string Name { get; }
	public string Version { get; }

	[ObservableProperty]
	private PluginState _state;

	public bool CanEnable => State is PluginState.Disabled or PluginState.Loaded or PluginState.Initialized or PluginState.Stopped;
	public bool CanDisable => State is PluginState.Running;
	public bool CanReload => State is not PluginState.NotLoaded;
	public bool CanUnload => State is not PluginState.NotLoaded;
}