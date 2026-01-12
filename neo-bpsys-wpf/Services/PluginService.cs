using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using System.IO;
using System.Reflection;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace neo_bpsys_wpf.Services;

public class PluginService : IPluginService
{
    public static readonly string PluginManifestFileName = "manifest.yml";

    internal static List<PluginInfo> InstalledPlugins { get; } = [];

    internal static Dictionary<Type, string> FrontedWindowAssemblyFolder { get; } = [];

    public static void InitializePlugins(HostBuilderContext context, IServiceCollection services)
    {
        if (!Directory.Exists(AppConstants.PluginPath))
        {
            Directory.CreateDirectory(AppConstants.PluginPath);
        }

        var deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var pluginDirs = Directory.EnumerateDirectories(AppConstants.PluginPath);

        if (Directory.Exists(AppConstants.BuiltInPluginPath))
        {
            pluginDirs = Directory.EnumerateDirectories(AppConstants.BuiltInPluginPath)
                .Concat(pluginDirs);
        }

        var newPath = Path.Combine(AppConstants.PluginPath, ".new");
        // 先处理覆盖安装的
        if (Directory.Exists(newPath))
        {
            var new_plugins = Directory.EnumerateDirectories(newPath);

            try
            {
                foreach (var plugin in new_plugins)
                {
                    var target = Path.Combine(AppConstants.PluginPath, Path.GetFileName(plugin));
                    MoveAndOverwrite(plugin, target);
                }

                Directory.Delete(newPath, true);
            }
            catch (Exception)
            {
                // ignored
            }
        }

        // 预处理插件信息
        foreach (var pluginDir in pluginDirs)
        {
            if (string.IsNullOrWhiteSpace(pluginDir) || pluginDir == ".new")
                continue;
            var manifestPath = Path.Combine(pluginDir, PluginManifestFileName);
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            var manifestYaml = File.ReadAllText(manifestPath);
            var manifest = deserializer.Deserialize<PluginManifest?>(manifestYaml);
            if (manifest == null)
            {
                continue;
            }

            var info = new PluginInfo
            {
                Manifest = manifest,
                IsLocal = true,
                PluginFolderPath = Path.GetFullPath(pluginDir),
                RealIconPath = Path.Combine(Path.GetFullPath(pluginDir), manifest.Icon),
                IsBuiltIn = Path.GetFullPath(pluginDir).Contains(AppConstants.BuiltInPluginPath)
            };
            if (info.IsUninstalling)
            {
                Directory.Delete(pluginDir, true);
                continue;
            }

            if (IPluginService.LoadedPluginsIds.Contains(manifest.Id))
                continue;
            IPluginService.LoadedPluginsIds.Add(manifest.Id);
            IPluginService.LoadedPluginsInternal.Add(info);
            if (!info.IsEnabled)
            {
                info.LoadStatus = PluginLoadStatus.Disabled;
            }

            if (info.IsEnabled && Version.TryParse(info.Manifest.ApiVersion, out var apiVersion) &&
                apiVersion < new Version(2, 0, 0, 0))
            {
                info.LoadStatus = PluginLoadStatus.Error;
                info.Exception =
                    new InvalidOperationException(
                        $"不兼容的 API 版本 {apiVersion}。插件的 API 版本需要至少为 2.0.0.0 才能被当前版本的 neo-bpsys-wpf 加载。");
            }
        }

        var loadOrder =
            IPluginService.LoadedPluginsInternal
                .Where(x => x.LoadStatus == PluginLoadStatus.NotLoaded)
                .Select(x => x.Manifest.Id)
                .ToList();

        Console.WriteLine($@"Resolved load order: {string.Join(", ", loadOrder)}");

        // 加载插件
        foreach (var id in loadOrder)
        {
            var info = IPluginService.LoadedPluginsInternal.First(x => x.Manifest.Id == id);
            var manifest = info.Manifest;
            var pluginDir = info.PluginFolderPath;
            try
            {
                var fullPath = Path.GetFullPath(Path.Combine(pluginDir, manifest.EntranceAssembly));
                var assembly = Assembly.LoadFrom(fullPath);
                var entrance = assembly.ExportedTypes
                    .FirstOrDefault(x => x.BaseType == typeof(PluginBase));

                if (entrance == null)
                {
                    continue;
                }

                if (Activator.CreateInstance(entrance) is not PluginBase entranceObj)
                {
                    continue;
                }

                // 通过反射获取插件程序集中带有 FrontedWindowInfo 特性的前台窗口类型
                foreach (var type in assembly.GetTypes())
                {
                    var frontedWindowInfoAttr = type.GetCustomAttribute<FrontedWindowInfo>();
                    if (frontedWindowInfoAttr != null)
                    {
                        // 将前台窗口类型和插件文件夹路径存储到字典中
                        FrontedWindowAssemblyFolder[type] = pluginDir;
                    }
                }

                entranceObj.PluginConfigFolder = pluginDir;
                if (!Directory.Exists(entranceObj.PluginConfigFolder))
                    Directory.CreateDirectory(entranceObj.PluginConfigFolder);


                entranceObj.Info = info;
                entranceObj.PluginConfigFolder = Path.Combine(AppConstants.PluginConfigsPath, info.Manifest.Id);
                entranceObj.Initialize(context, services);
                services.AddSingleton(entranceObj);
                services.AddSingleton(entrance, entranceObj);
                info.LoadStatus = PluginLoadStatus.Loaded;
                InstalledPlugins.Add(info);

                Console.WriteLine($@"Initialize plugin: {pluginDir} ({manifest.Version})");
            }
            catch (Exception ex)
            {
                info.Exception = ex;
                info.LoadStatus = PluginLoadStatus.Error;
            }
        }
    }

    private static void MoveAndOverwrite(string source, string target)
    {
        Directory.CreateDirectory(target);

        foreach (var file in Directory.GetFiles(source))
        {
            string destFile = Path.Combine(target, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
        }

        foreach (var dir in Directory.GetDirectories(source))
        {
            string destDir = Path.Combine(target, Path.GetFileName(dir));
            MoveAndOverwrite(dir, destDir);
        }

        Directory.Delete(source, true);
    }
}