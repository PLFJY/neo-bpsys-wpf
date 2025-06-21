using neo_bpsys_wpf.Abstractions.Services;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using neo_bpsys_wpf.Models;

namespace neo_bpsys_wpf.Services
{
    public class SettingsHostService : ISettingsHostService
    {
        public Settings Settings { get; set; } = new();
        private readonly IMessageBoxService _messageBoxService;
        private const string SettingFileName = "Config.json";
        private readonly string _settingFileDefaultPath;
        private readonly string _settingFilePath;
        private readonly string _settingFileDirectory;

        public SettingsHostService(IMessageBoxService messageBoxService)
        {
            _messageBoxService = messageBoxService;
            _settingFileDefaultPath = Path.Combine(Environment.CurrentDirectory, "DefaultConfig.json");
            _settingFileDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "neo-bpsys-wpf");
            _settingFilePath = Path.Combine(_settingFileDirectory, SettingFileName);
            LoadConfig();
        }

        public void SaveConfig()
        {
            if (!Directory.Exists(_settingFileDirectory))
                Directory.CreateDirectory(_settingFileDirectory);
            
            File.WriteAllText(_settingFilePath, JsonSerializer.Serialize(Settings));
        }
        public void LoadConfig()
        {
            if (!File.Exists(_settingFilePath))
                ResetConfig();
            var json = File.ReadAllText(_settingFilePath);
            try
            {
                Settings = JsonSerializer.Deserialize<Settings>(json)!;
            }
            catch (JsonException e)
            {
                _messageBoxService.ShowErrorAsync($"读取配置文件错误\n{e.Message}");
                File.Delete(_settingFilePath);
                ResetConfig();
            }
        }

        public void ResetConfig()
        {
            if (!File.Exists(_settingFileDefaultPath)) return;
            try
            {
                if (!Directory.Exists(_settingFileDirectory))
                    Directory.CreateDirectory(_settingFileDirectory);
                
                File.Copy(_settingFileDefaultPath, _settingFilePath);
                LoadConfig();
            }
            catch (Exception e)
            {
                _messageBoxService.ShowErrorAsync($"重置配置文件错误\n{e.Message}");
                throw;
            }
        }
    }
}