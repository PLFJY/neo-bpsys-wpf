using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Downloader;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Controls;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Views.Windows;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using WPFLocalizeExtension.Engine;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class SettingPageViewModel : ViewModelBase
{
    #region 前台UI导入导出

    //==============================================================
    // UI 文件目录结构:
    // *.bpui/
    // ├── CustomUi/
    // ├── FrontElementsConfig/
    // └── Config.json
    // 导出过程：先触发一次UI保存逻辑，复制 Config.json 到临时目录
    // 导入UI对象，利用反射拿到所有自定义UI的路径，复制所有自定义UI文件到临时目录
    // 复制前台元素位置文件到临时目录
    // 打包，改名，输出
    // 
    // 导入过程: 读取文件，解压，复制，覆盖，删除
    //==============================================================

    /// <summary>
    /// 临时文件路径
    /// </summary>
    private static readonly string TempPath = Path.Combine(AppConstants.AppTempPath, "UiPackage");

    /// <summary>
    /// 自定义UI临时文件路径
    /// </summary>
    private static readonly string CustomUiTempPath = Path.Combine(TempPath, "CustomUi");

    /// <summary>
    /// 配置临时文件路径
    /// </summary>
    private static readonly string ConfigTempPath = Path.Combine(TempPath, "Config.json");

    /// <summary>
    /// 前台元素位置临时文件路径
    /// </summary>
    private static readonly string FrontElementsConfigTempPath = Path.Combine(TempPath, "FrontElementsConfig");

    /// <summary>
    /// 导出UI配置
    /// </summary>
    [RelayCommand]
    private async Task ExportUiConfigAsync()
    {
        var savePath = _filePickerService.SaveBpuiFile("saved_ui");
        if (string.IsNullOrWhiteSpace(savePath))
            return;
        //先保存一遍配置保证地址格式已被转换
        await _settingsHostService.SaveConfigAsync();
        try
        {
            //创建临时文件夹
            if (Directory.Exists(TempPath))
                Directory.Delete(TempPath, true);

            Directory.CreateDirectory(TempPath);

            //复制Config文件
            File.Copy(AppConstants.ConfigFilePath, ConfigTempPath);
            //复制自定义UI
            CopyCustomUiToTemp(_settingsHostService.Settings, CustomUiTempPath);
            //复制前台配置文件
            foreach (var valueTuple in _frontedWindowService.FrontedCanvas)
            {
                var windowName = _frontedWindowService.GetWindowName(valueTuple.Item1);
                if (windowName == null) continue;
                CopyFrontElementsPositionFileToTemp(windowName, valueTuple.Item2);
            }

            //打包
            var zipPath = Path.Combine(AppConstants.AppTempPath, Path.GetFileName(savePath));
            if (File.Exists(zipPath)) File.Delete(zipPath);
            ZipFile.CreateFromDirectory(TempPath, zipPath);
            //保存
            if (File.Exists(savePath))
            {
                if (await MessageBoxHelper.ShowConfirmAsync(
                        $"{savePath} {I18nHelper.GetLocalizedString("PathHasAlreadyExistAreYouSureToCoverIt")}",
                        I18nHelper.GetLocalizedString("CoverTip"), I18nHelper.GetLocalizedString("Confirm"),
                        I18nHelper.GetLocalizedString("Cancel")))
                    File.Delete(savePath);
                else
                {
                    //删除作案痕迹
                    Directory.Delete(TempPath, true);
                    File.Delete(zipPath);
                    return;
                }
            }

            File.Copy(zipPath, savePath);
            //删除作案痕迹
            Directory.Delete(TempPath, true);
            File.Delete(zipPath);
            //提示用户已完成
            await MessageBoxHelper.ShowInfoAsync(
                $"{I18nHelper.GetLocalizedString("UIConfigurationHasBeenSavedTo")} {savePath}");
        }
        catch (Exception e)
        {
            await MessageBoxHelper.ShowErrorAsync(e.Message, I18nHelper.GetLocalizedString("UIPackingError"));
        }
    }

    /// <summary>
    /// 复制自定义UI位置文件
    /// </summary>
    /// <param name="windowName">窗口名称</param>
    /// <param name="canvasName">画布名称</param>
    private static void CopyFrontElementsPositionFileToTemp(string windowName,
        string canvasName = "BaseCanvas")
    {
        var path = Path.Combine(AppConstants.AppDataPath, $"{windowName}Config-{canvasName}.json");
        var destPath = Path.Combine(FrontElementsConfigTempPath, $"{windowName}Config-{canvasName}.json");
        if (!Directory.Exists(FrontElementsConfigTempPath)) Directory.CreateDirectory(FrontElementsConfigTempPath);
        if (File.Exists(path))
            File.Copy(path, destPath);
    }

    /// <summary>
    /// 复制自定义UI图片文件
    /// </summary>
    /// <param name="settings">设置</param>
    /// <param name="targetPath">目标路径</param>
    private static void CopyCustomUiToTemp(Settings settings, string targetPath)
    {
        var paths = CollectValidImagePathsIterative(settings);
        if (!Directory.Exists(targetPath)) Directory.CreateDirectory(targetPath);
        foreach (var path in paths)
        {
            File.Copy(path, Path.Combine(targetPath, Path.GetFileName(path)), true);
        }
    }

    /// <summary>
    /// 递归获取有效的图片路径
    /// </summary>
    /// <param name="root">根对象</param>
    /// <returns>有效的图片路径</returns>
    private static HashSet<string> CollectValidImagePathsIterative(object root)
    {
        var validPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visitedObjects = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var queue = new Queue<object>();

        queue.Enqueue(root);
        visitedObjects.Add(root);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var type = current.GetType();

            // 仅处理我们关心的命名空间（避免进入WPF内部对象）
            if (!type.Namespace?.StartsWith("neo_bpsys_wpf.Core.Models") == true)
                continue;

            // 处理当前对象的所有属性
            foreach (var prop in GetRelevantProperties(type))
            {
                try
                {
                    var value = prop.GetValue(current);

                    // 处理字符串属性（图片URI）
                    if (prop.PropertyType == typeof(string))
                    {
                        ProcessStringProperty(value as string, validPaths);
                    }
                    // 处理嵌套对象
                    else if (value != null &&
                             !visitedObjects.Contains(value) &&
                             !IsWpfResourceType(prop.PropertyType))
                    {
                        visitedObjects.Add(value);
                        queue.Enqueue(value);
                    }
                }
                catch
                {
                    // 忽略无法访问的属性
                }
            }
        }

        return validPaths;
    }

    /// <summary>
    /// 获取所有可访问的属性路径
    /// </summary>
    /// <param name="type">类型</param>
    /// <returns>属性信息列表</returns>
    private static IEnumerable<PropertyInfo> GetRelevantProperties(Type type)
    {
        // 仅获取：公共实例属性 + 非索引器 + (字符串或自定义类型)
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0 &&
                        (p.PropertyType == typeof(string) ||
                         !p.PropertyType.IsValueType));
    }

    /// <summary>
    /// 获取所有可序列化的属性
    /// </summary>
    /// <param name="path">路径</param>
    /// <param name="validPaths">有效路径列表</param>
    private static void ProcessStringProperty(string? path, HashSet<string> validPaths)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        // 排除WPF资源路径
        if (path.StartsWith("pack://", StringComparison.OrdinalIgnoreCase))
            return;

        // 排除颜色代码（#FFFFFFFF）
        if (Regex.IsMatch(path, "^#[0-9A-Fa-f]{6,8}$"))
            return;

        // 处理环境变量
        var expandedPath = Environment.ExpandEnvironmentVariables(path);

        // 规范化路径
        if (TryNormalizePath(expandedPath, out var normalizedPath) &&
            File.Exists(normalizedPath))
        {
            validPaths.Add(normalizedPath);
        }
    }

    /// <summary>
    /// 是否是WPF的资源类型，如果是返回否，不进行递归
    /// </summary>
    /// <param name="type">类型</param>
    /// <returns>是否是WPF的资源类型</returns>
    private static bool IsWpfResourceType(Type type)
    {
        // 排除WPF资源类型，防止进入复杂对象图
        return type.Namespace?.StartsWith("System.Windows") == true ||
               type.Namespace?.StartsWith("System.Media") == true ||
               type == typeof(FontFamily) ||
               type == typeof(Brush) ||
               type == typeof(ImageSource);
    }

    /// <summary>
    /// 尝试使路径规则化
    /// </summary>
    /// <param name="inputPath">输入路径</param>
    /// <param name="normalizedPath">正规化后的路径</param>
    /// <returns>是否成功</returns>
    private static bool TryNormalizePath(string inputPath, out string? normalizedPath)
    {
        normalizedPath = null;

        try
        {
            // 获取绝对路径，确保路径斜线格式正确
            var cleanPath = Path.GetFullPath(
                inputPath.Replace('/', Path.DirectorySeparatorChar)
                    .Replace('\\', Path.DirectorySeparatorChar)
            );

            // 验证路径是否在应用程序目录或用户目录内（安全检查）
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var userDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            if (cleanPath.StartsWith(appDir, StringComparison.OrdinalIgnoreCase) ||
                cleanPath.StartsWith(userDir, StringComparison.OrdinalIgnoreCase))
            {
                normalizedPath = cleanPath;
                return true;
            }
        }
        catch
        {
            // ignored
        }

        return false;
    }

    /// <summary>
    /// 导入UI配置
    /// </summary>
    [RelayCommand]
    private async Task ImportUiConfigAsync()
    {
        //准备ui文件路径
        var uiFilePath = _filePickerService.PickBpuiFile();

        if (uiFilePath == null) return;

        //如果存在了文件夹直接删除
        if (Directory.Exists(TempPath))
            Directory.Delete(TempPath, true);

        try
        {
            //解压UI包
            ZipFile.ExtractToDirectory(uiFilePath, TempPath);

            //拷贝配置文件
            File.Copy(ConfigTempPath, AppConstants.ConfigFilePath, true);

            //拷贝自定义UI图片
            var customUiFiles = Directory.GetFiles(CustomUiTempPath);
            if (!Directory.Exists(AppConstants.CustomUiPath))
                Directory.CreateDirectory(AppConstants.CustomUiPath);
            foreach (var customUiFile in customUiFiles)
            {
                File.Copy(customUiFile, Path.Combine(AppConstants.CustomUiPath, Path.GetFileName(customUiFile)), true);
            }

            //拷贝前台位置配置文件
            var frontElementConfigures = Directory.GetFiles(FrontElementsConfigTempPath);
            foreach (var frontElementConfigure in frontElementConfigures)
            {
                File.Copy(frontElementConfigure,
                    Path.Combine(AppConstants.AppDataPath, Path.GetFileName(frontElementConfigure)), true);
            }

            //清理作案痕迹
            Directory.Delete(TempPath, true);

            //告诉用户已经导入完了
            await MessageBoxHelper.ShowInfoAsync(I18nHelper.GetLocalizedString("UIImportIsFinished"),
                I18nHelper.GetLocalizedString("UIImportTip"));

            //重启应用程序
            AppBase.Current.Restart();
        }
        catch (Exception e)
        {
            await MessageBoxHelper.ShowErrorAsync(e.Message, I18nHelper.GetLocalizedString("UIPackLoadingError"));
        }
    }

    #endregion
}

