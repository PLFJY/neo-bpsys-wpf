using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Abstractions.ViewModels;
using neo_bpsys_wpf.Core.Plugins;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class ExtensionPageViewModel : ViewModelBase
{
	public ExtensionPageViewModel()
	{
		//Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
	}

	private readonly IPluginManager _pluginManager = null!;
	private readonly IMessageBoxService _messageBoxService = null!;

	public ObservableCollection<PluginEntryViewModel> Plugins { get; } = new();

	[ObservableProperty] private bool _isBusy;
	[ObservableProperty] private string _statusText = string.Empty;

	public ExtensionPageViewModel(IPluginManager pluginManager, IMessageBoxService messageBoxService)
	{
		_pluginManager = pluginManager;
		_messageBoxService = messageBoxService;
	}

	public async Task RefreshAsync()
	{
		if (IsBusy) return;
		IsBusy = true;

		try
		{
			Directory.CreateDirectory(AppConstants.PluginsPath);

			var discovered = await _pluginManager.DiscoverPluginsAsync(AppConstants.PluginsPath);
			var loadedMetadata = _pluginManager.AllPluginMetadata;

			// 以 AssemblyPath 作为主要键，避免“发现的Id”和“实际加载的Id”不一致导致重复条目
			var entriesByKey = new Dictionary<string, PluginEntryViewModel>(StringComparer.OrdinalIgnoreCase);

			foreach (var metadata in discovered)
			{
				var key = GetEntryKey(metadata);
				if (!entriesByKey.ContainsKey(key))
				{
					entriesByKey[key] = PluginEntryViewModel.From(metadata, isLoaded: false, isEnabled: false);
				}
			}

			foreach (var metadata in loadedMetadata)
			{
				var key = GetEntryKey(metadata);
				var isLoaded = _pluginManager.IsPluginLoaded(metadata.Id);
				var isEnabled = _pluginManager.IsPluginEnabled(metadata.Id);
				// 已加载信息覆盖发现信息
				entriesByKey[key] = PluginEntryViewModel.From(metadata, isLoaded, isEnabled);
			}

			Plugins.Clear();
			foreach (var entry in entriesByKey.Values
						 .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
						 .ThenBy(e => e.Id, StringComparer.OrdinalIgnoreCase))
			{
				Plugins.Add(entry);
			}

			StatusText = $"插件目录：{AppConstants.PluginsPath}  |  发现：{discovered.Count}  |  已加载：{_pluginManager.LoadedPlugins.Count}";
		}
		catch (Exception ex)
		{
			StatusText = $"刷新失败：{ex.Message}";
			await _messageBoxService.ShowErrorAsync($"刷新插件列表失败\n{ex.Message}");
		}
		finally
		{
			IsBusy = false;
		}
	}

	[RelayCommand]
	private async Task Refresh()
	{
		await RefreshAsync();
	}

	[RelayCommand]
	private async Task LoadAll()
	{
		if (IsBusy) return;
		IsBusy = true;
		try
		{
			Directory.CreateDirectory(AppConstants.PluginsPath);
			var results = await _pluginManager.LoadPluginsAsync(AppConstants.PluginsPath);
			var successCount = results.Count(r => r.Success);
			var failCount = results.Count - successCount;
			StatusText = $"加载完成：成功 {successCount}，失败 {failCount}";
		}
		catch (Exception ex)
		{
			await _messageBoxService.ShowErrorAsync($"加载全部插件失败\n{ex.Message}");
		}
		finally
		{
			IsBusy = false;
			await RefreshAsync();
		}
	}

	[RelayCommand]
	private void OpenPluginFolder()
	{
		try
		{
			Directory.CreateDirectory(AppConstants.PluginsPath);
			Process.Start(new ProcessStartInfo("explorer.exe", AppConstants.PluginsPath)
			{
				UseShellExecute = true,
			});
		}
		catch (Exception ex)
		{
			_ = _messageBoxService.ShowErrorAsync($"打开插件目录失败\n{ex.Message}");
		}
	}

	[RelayCommand]
	private async Task LoadOrReload(PluginEntryViewModel? entry)
	{
		if (entry == null || IsBusy) return;
		IsBusy = true;

		try
		{
			var pluginId = ResolvePluginId(entry);

			PluginLoadResult result;
			if (_pluginManager.IsPluginLoaded(pluginId))
			{
				result = await _pluginManager.ReloadPluginAsync(pluginId);
			}
			else
			{
				if (string.IsNullOrWhiteSpace(entry.AssemblyPath))
				{
					await _messageBoxService.ShowErrorAsync("该插件缺少程序集路径，无法加载。\n请确认插件 DLL 在插件目录中，或提供 .plugin.json 清单。");
					return;
				}
				result = await _pluginManager.LoadPluginAsync(entry.AssemblyPath);
			}

			if (!result.Success)
			{
				await _messageBoxService.ShowErrorAsync($"操作失败\n{result.ErrorMessage ?? result.Exception?.Message ?? "未知错误"}");
			}
		}
		catch (Exception ex)
		{
			await _messageBoxService.ShowErrorAsync($"操作失败\n{ex.Message}");
		}
		finally
		{
			IsBusy = false;
			await RefreshAsync();
		}
	}

	[RelayCommand]
	private async Task ToggleEnabled(PluginEntryViewModel? entry)
	{
		if (entry == null || IsBusy) return;
		IsBusy = true;

		try
		{
			var desiredEnable = !entry.IsEnabled;
			var pluginId = ResolvePluginId(entry);

			if (!_pluginManager.IsPluginLoaded(pluginId))
			{
				if (string.IsNullOrWhiteSpace(entry.AssemblyPath))
				{
					await _messageBoxService.ShowErrorAsync("该插件未加载且缺少程序集路径，无法启用。\n请先放入插件目录。");
					return;
				}

				var load = await _pluginManager.LoadPluginAsync(entry.AssemblyPath);
				if (!load.Success)
				{
					await _messageBoxService.ShowErrorAsync($"加载失败\n{load.ErrorMessage ?? load.Exception?.Message ?? "未知错误"}");
					return;
				}

				if (load.Metadata != null && !string.IsNullOrWhiteSpace(load.Metadata.Id))
				{
					pluginId = load.Metadata.Id;
				}
			}

			var currentlyEnabled = _pluginManager.IsPluginEnabled(pluginId);

			if (desiredEnable)
			{
				if (currentlyEnabled)
				{
					return;
				}

				var ok = await _pluginManager.EnablePluginAsync(pluginId);
				if (!ok)
				{
					var meta = _pluginManager.GetPluginMetadata(pluginId);
					var detail = meta?.LoadError;
					await _messageBoxService.ShowErrorAsync(string.IsNullOrWhiteSpace(detail)
						? "操作失败：插件状态未改变（可能存在依赖关系或插件内部拒绝）。"
						: $"操作失败：{detail}");
				}

				return;
			}

			// desired disable
			if (!currentlyEnabled)
			{
				return;
			}

			{
				var ok = await _pluginManager.DisablePluginAsync(pluginId);
				if (!ok)
				{
					var meta = _pluginManager.GetPluginMetadata(pluginId);
					var detail = meta?.LoadError;
					await _messageBoxService.ShowErrorAsync(string.IsNullOrWhiteSpace(detail)
						? "操作失败：插件状态未改变（可能存在依赖关系或插件内部拒绝）。"
						: $"操作失败：{detail}");
				}
			}
		}
		catch (Exception ex)
		{
			await _messageBoxService.ShowErrorAsync($"操作失败\n{ex.Message}");
		}
		finally
		{
			IsBusy = false;
			await RefreshAsync();
		}
	}

	[RelayCommand]
	private async Task Unload(PluginEntryViewModel? entry)
	{
		if (entry == null || IsBusy) return;
		IsBusy = true;

		try
		{
			var pluginId = ResolvePluginId(entry);

			if (!_pluginManager.IsPluginLoaded(pluginId))
			{
				return;
			}

			var ok = await _pluginManager.UnloadPluginAsync(pluginId);
			if (!ok)
			{
				await _messageBoxService.ShowErrorAsync("卸载失败：可能有其他插件依赖它，或插件拒绝卸载。");
			}
		}
		catch (Exception ex)
		{
			await _messageBoxService.ShowErrorAsync($"卸载失败\n{ex.Message}");
		}
		finally
		{
			IsBusy = false;
			await RefreshAsync();
		}
	}

	public sealed partial class PluginEntryViewModel : ObservableObject
	{
		[ObservableProperty] private string _id = string.Empty;
		[ObservableProperty] private string _name = string.Empty;
		[ObservableProperty] private string _versionText = string.Empty;
		[ObservableProperty] private string _author = string.Empty;
		[ObservableProperty] private string _assemblyPath = string.Empty;
		[ObservableProperty] private bool _isEnabled;
		[ObservableProperty] private string _loadStateText = string.Empty;
		[ObservableProperty] private string? _loadError;
		[ObservableProperty] private bool _isLoaded;

		public bool CanUnload => IsLoaded;

		public string LoadOrReloadText => IsLoaded ? "重载" : "加载";

		public string ToggleEnabledText => IsEnabled ? "禁用" : "启用";

		public static PluginEntryViewModel From(PluginMetadata metadata, bool isLoaded, bool isEnabled)
		{
			var vm = new PluginEntryViewModel
			{
				Id = metadata.Id,
				Name = metadata.Name,
				VersionText = metadata.Version.ToString(),
				Author = string.IsNullOrWhiteSpace(metadata.Author) ? "Unknown" : metadata.Author,
				AssemblyPath = metadata.AssemblyPath,
				IsEnabled = isEnabled,
				IsLoaded = isLoaded,
				LoadError = metadata.LoadError,
				LoadStateText = ToText(metadata.LoadState, isLoaded),
			};

			return vm;
		}

		private static string ToText(PluginLoadState state, bool isLoaded)
		{
			if (isLoaded)
				return "已加载";

			return state switch
			{
				PluginLoadState.NotLoaded => "未加载",
				PluginLoadState.Loading => "加载中",
				PluginLoadState.Failed => "失败",
				PluginLoadState.Disabled => "已禁用",
				PluginLoadState.Unloaded => "已卸载",
				PluginLoadState.Loaded => "已加载",
				_ => state.ToString()
			};
		}
	}

	private static string GetEntryKey(PluginMetadata metadata)
	{
		if (!string.IsNullOrWhiteSpace(metadata.AssemblyPath))
		{
			return "path:" + NormalizePath(metadata.AssemblyPath);
		}

		return "id:" + metadata.Id;
	}

	private string ResolvePluginId(PluginEntryViewModel entry)
	{
		// 首选当前条目的 Id
		if (_pluginManager.GetPluginMetadata(entry.Id) != null)
		{
			return entry.Id;
		}

		// 如果 Id 不匹配（常见于缺少 manifest 的发现），按 AssemblyPath 找到真实 Id
		if (string.IsNullOrWhiteSpace(entry.AssemblyPath))
		{
			return entry.Id;
		}

		var targetPath = NormalizePath(entry.AssemblyPath);
		var match = _pluginManager.AllPluginMetadata.FirstOrDefault(m =>
			!string.IsNullOrWhiteSpace(m.AssemblyPath) &&
			string.Equals(NormalizePath(m.AssemblyPath), targetPath, StringComparison.OrdinalIgnoreCase));

		return match?.Id ?? entry.Id;
	}

	private static string NormalizePath(string path)
	{
		try
		{
			return Path.GetFullPath(path.Trim());
		}
		catch
		{
			return path.Trim();
		}
	}
}