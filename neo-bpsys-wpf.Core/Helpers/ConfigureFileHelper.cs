using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace neo_bpsys_wpf.Core.Helpers;

public static class ConfigureFileHelper
{
    
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    static ConfigureFileHelper()
    {
        // 覆盖默认的 JsonSerializerOptions
        var field = typeof(JsonSerializerOptions)?.GetField("s_defaultOptions",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        field?.SetValue(null, JsonSerializerOptions);
        if (Directory.Exists(AppConstants.PluginConfigsPath))
        {
            Directory.CreateDirectory(AppConstants.PluginConfigsPath);
        }
    }

    /// <summary>
    /// 加载配置文件
    /// </summary>
    /// <param name="path">配置文件路径</param>
    /// <typeparam name="T">配置文件类型</typeparam>
    /// <returns>配置文件对象</returns>
    public static T LoadConfig<T>(string path)
    {
        T obj;
        try
        {
            if (!File.Exists(path))
            {
                obj = Activator.CreateInstance<T>();
                SaveConfig(path, obj);
                return obj;
            }

            var json = File.ReadAllText(path);
            var r = JsonSerializer.Deserialize<T>(json, JsonSerializerOptions);
            if (r != null) return r;
            obj = Activator.CreateInstance<T>();
            SaveConfig(path, obj);
            return obj;
        }
        catch (Exception e)
        {
            _ = MessageBoxHelper.ShowErrorAsync($"读取插件配置时错误\nError occur when loading plugin configure file\n{e.Message}");
        }

        obj = Activator.CreateInstance<T>();
        SaveConfig(path, obj);
        return obj;
    }

    /// <summary>
    /// 保存配置文件
    /// </summary>
    /// <param name="path">配置文件路径</param>
    /// <param name="o">配置文件对象</param>
    /// <typeparam name="T">配置文件类型</typeparam>
    public static void SaveConfig<T>(string path, T o)
    {
        WriteAllTextSafe(path, JsonSerializer.Serialize<T>(o, JsonSerializerOptions));
    }

    private static void WriteAllTextSafe(string path, string content)
    {
        var dirPath = Path.GetDirectoryName(path)!;
        if (!Directory.Exists(dirPath))
        {
            Directory.CreateDirectory(dirPath);
        }
        using var stream = new FileStream(path, FileMode.Create);
        using var writer = new StreamWriter(stream);
        writer.Write(content);
        stream.Flush(true);
    }
}