using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;

namespace neo_bpsys_wpf.GameStopwatch.Services;

/// <summary>
/// 使用 WPF DispatcherTimer 刷新比赛秒表的服务。
/// </summary>
public sealed class GameStopwatchService : IGameStopwatchService, IDisposable
{
    private readonly Stopwatch _stopwatch = new();
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _settingsSaveTimer;
    private readonly string _settingsPath;
    private string _textColor = "#FFFFFFFF";
    private string _fontFamilyName = "Arial";
    private double _fontSize = 48;
    private double _windowSize = 320;
    private bool _settingsDirty;

    /// <summary>初始化秒表服务。</summary>
    /// <param name="settingsPath">设置文件路径。</param>
    public GameStopwatchService(string settingsPath)
    {
        _settingsPath = settingsPath;
        LoadSettings();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += (_, _) => OnPropertyChanged(nameof(DisplayText));
        _settingsSaveTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(250),
            DispatcherPriority.Background,
            (_, _) => FlushSettings(),
            Dispatcher.CurrentDispatcher);
        _settingsSaveTimer.Stop();
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <inheritdoc />
    public string DisplayText => $"{(long)_stopwatch.Elapsed.TotalMinutes:00}:{_stopwatch.Elapsed.Seconds:00}";

    /// <inheritdoc />
    public bool IsRunning => _stopwatch.IsRunning;

    /// <inheritdoc />
    public string TextColor
    {
        get => _textColor;
        set => SetSetting(ref _textColor, value, nameof(TextColor));
    }

    /// <inheritdoc />
    public string FontFamilyName
    {
        get => _fontFamilyName;
        set => SetSetting(ref _fontFamilyName, value, nameof(FontFamilyName));
    }

    /// <inheritdoc />
    public double FontSize
    {
        get => _fontSize;
        set
        {
            var normalized = Math.Clamp(value, 12, 300);
            SetSetting(ref _fontSize, normalized, nameof(FontSize));
        }
    }

    /// <inheritdoc />
    public double WindowSize
    {
        get => _windowSize;
        set => SetSetting(ref _windowSize, Math.Clamp(value, 80, 4096), nameof(WindowSize));
    }

    /// <inheritdoc />
    /// <inheritdoc />
    public void Start()
    {
        _stopwatch.Start();
        _timer.Start();
        OnPropertyChanged(nameof(IsRunning));
    }

    /// <inheritdoc />
    public void Pause()
    {
        _stopwatch.Stop();
        _timer.Stop();
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(DisplayText));
    }

    /// <inheritdoc />
    public void Reset()
    {
        _stopwatch.Reset();
        _timer.Stop();
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(DisplayText));
    }

    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(_settingsPath)) return;
            var settings = JsonSerializer.Deserialize<StopwatchSettings>(File.ReadAllText(_settingsPath));
            if (settings is null) return;
            // 手工编辑的 Settings.json 可能把文本字段写成 null；回退默认值，避免颜色/字体解析在绑定 getter 中抛异常。
            _textColor = string.IsNullOrWhiteSpace(settings.TextColor) ? _textColor : settings.TextColor;
            _fontFamilyName = string.IsNullOrWhiteSpace(settings.FontFamilyName) ? _fontFamilyName : settings.FontFamilyName;
            _fontSize = Math.Clamp(settings.FontSize, 12, 300);
            _windowSize = settings.WindowSize is null ? 320 : Math.Clamp(settings.WindowSize.Value, 80, 4096);
        }
        catch (Exception)
        {
            // 设置损坏时保留代码中的明确默认值，不猜测旧字段含义。
        }
    }

    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(new StopwatchSettings(
                _textColor, _fontFamilyName, _fontSize, _windowSize)));
            _settingsDirty = false;
        }
        catch (Exception ex)
        {
            // 设置写入失败不应打断拖动色盘等交互；保留脏标记，等待下一次重试。
            _settingsDirty = true;
            Debug.WriteLine($"[GameStopwatch] 保存设置失败: {ex.Message}");
        }
    }

    private void ScheduleSave()
    {
        _settingsDirty = true;
        _settingsSaveTimer.Stop();
        _settingsSaveTimer.Start();
    }

    private void FlushSettings()
    {
        _settingsSaveTimer.Stop();
        if (!_settingsDirty) return;
        SaveSettings();
    }

    private void SetSetting<T>(ref T field, T value, string propertyName)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        // 色盘拖动会连续产生大量属性变化，合并为一次磁盘写入，避免每次移动都同步读写设置文件。
        ScheduleSave();
        OnPropertyChanged(propertyName);
    }

    /// <summary>释放计时器，并立即落盘尚未保存的设置。</summary>
    public void Dispose()
    {
        _timer.Stop();
        _settingsSaveTimer.Stop();
        FlushSettings();
    }

    private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed record StopwatchSettings(
        string TextColor,
        string FontFamilyName,
        double FontSize,
        double? WindowSize)
    {
        public StopwatchSettings() : this("#FFFFFFFF", "Arial", 48, null) { }
    }
}
