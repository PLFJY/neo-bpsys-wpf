using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Abstractions.ViewModels;
using neo_bpsys_wpf.Core.Models;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using Sdcb.PaddleInference;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models;
using System.Windows;
using System.Windows.Threading;
using System.Linq;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class OcrHelperPageViewModel : ViewModelBase
    , IDisposable
{
    public OcrHelperPageViewModel() { }

    private readonly ISharedDataService _sharedDataService;
    private readonly ISettingsHostService _settingsHostService;
    private readonly IOcrModelService _ocrModelService;

    private DispatcherTimer? _ocrTimer;
    private PaddleOcrAll? _ocrAll;
    private CancellationTokenSource? _ocrDownloadCts;
    private bool _isOcrRunning;
    private bool _preferMkldnn = true;
    private int _ocrRunCounter;
    private const int OcrRecycleThreshold = 60;

    [ObservableProperty] private bool _isOcrRecognizing;
    [ObservableProperty] private bool _isOcrModelDownloading;
    [ObservableProperty] private bool _isPickRowModeEnabled;
    [ObservableProperty] private bool _isBanRowModeEnabled;

    public OcrHelperPageViewModel(ISharedDataService sharedDataService, ISettingsHostService settingsHostService, IOcrModelService ocrModelService)
    {
        _sharedDataService = sharedDataService;
        _settingsHostService = settingsHostService;
        _ocrModelService = ocrModelService;
        _isPickRowModeEnabled = _settingsHostService.Settings.BpWindowSettings.PickOcrRowMode;
        _isBanRowModeEnabled = _settingsHostService.Settings.BpWindowSettings.BanOcrRowMode;
    }

    [RelayCommand]
    private void SelectPickOcrRowRegions()
    {
        var labels = new[] { "框选求生者一排", "框选监管者" };
        var win = new neo_bpsys_wpf.Views.Windows.RegionSelectorWindow(labels)
        {
            Owner = Application.Current.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        win.ShowDialog();
        var regions = win.Regions;
        if (regions.Count >= 2)
        {
            var bp = _settingsHostService.Settings.BpWindowSettings;
            bp.PickOcrRowRegions = [ regions[0], regions[1] ];
            bp.PickOcrRowMode = true;
            IsPickRowModeEnabled = true;
            if (IsOcrRecognizing) StartOcrTimer();
        }
    }

    [RelayCommand]
    private void SelectBanSurOcrRowRegion()
    {
        var labels = new[] { "框选当局Ban（求生者）一排" };
        var win = new neo_bpsys_wpf.Views.Windows.RegionSelectorWindow(labels)
        {
            Owner = Application.Current.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        win.ShowDialog();
        var regions = win.Regions;
        if (regions.Count >= 1)
        {
            var bp = _settingsHostService.Settings.BpWindowSettings;
            bp.BanSurOcrRowRegion = regions[0];
            bp.BanOcrRowMode = true;
            IsBanRowModeEnabled = true;
            if (IsOcrRecognizing) StartOcrTimer();
        }
    }

    [RelayCommand]
    private void SelectBanHunOcrRowRegion()
    {
        var labels = new[] { "框选当局Ban（监管者）一排" };
        var win = new neo_bpsys_wpf.Views.Windows.RegionSelectorWindow(labels)
        {
            Owner = Application.Current.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        win.ShowDialog();
        var regions = win.Regions;
        if (regions.Count >= 1)
        {
            var bp = _settingsHostService.Settings.BpWindowSettings;
            bp.BanHunOcrRowRegion = regions[0];
            bp.BanOcrRowMode = true;
            IsBanRowModeEnabled = true;
            if (IsOcrRecognizing) StartOcrTimer();
        }
    }

    partial void OnIsPickRowModeEnabledChanged(bool value)
    {
        _settingsHostService.Settings.BpWindowSettings.PickOcrRowMode = value;
        if (!IsOcrRecognizing) return;
        var bp = _settingsHostService.Settings.BpWindowSettings;
        if (value)
        {
            if (bp.PickOcrRowRegions == null || bp.PickOcrRowRegions.Count != 2)
            {
                SelectPickOcrRowRegions();
                return;
            }
        }
        StopOcrTimer();
        StartOcrTimer();
    }

    partial void OnIsBanRowModeEnabledChanged(bool value)
    {
        _settingsHostService.Settings.BpWindowSettings.BanOcrRowMode = value;
        if (!IsOcrRecognizing) return;
        var bp = _settingsHostService.Settings.BpWindowSettings;
        if (value)
        {
            if (bp.BanSurOcrRowRegion.Width <= 0 || bp.BanSurOcrRowRegion.Height <= 0)
            {
                SelectBanSurOcrRowRegion();
                return;
            }
            if (bp.BanHunOcrRowRegion.Width <= 0 || bp.BanHunOcrRowRegion.Height <= 0)
            {
                SelectBanHunOcrRowRegion();
                return;
            }
        }
        StopOcrTimer();
        StartOcrTimer();
    }

    partial void OnIsOcrRecognizingChanged(bool value)
    {
        if (value)
        {
            var bp = _settingsHostService.Settings.BpWindowSettings;
            if (IsPickRowModeEnabled)
            {
                var rects = bp.PickOcrRowRegions;
                if (rects == null || rects.Count != 2)
                {
                    SelectPickOcrRowRegions();
                    return;
                }
            }
            if (IsBanRowModeEnabled)
            {
                var surOk = bp.BanSurOcrRowRegion.Width > 0 && bp.BanSurOcrRowRegion.Height > 0;
                var hunOk = bp.BanHunOcrRowRegion.Width > 0 && bp.BanHunOcrRowRegion.Height > 0;
                if (!surOk)
                {
                    SelectBanSurOcrRowRegion();
                    return;
                }
                if (!hunOk)
                {
                    SelectBanHunOcrRowRegion();
                    return;
                }
            }
            StartOcrTimer();
        }
        else
        {
            StopOcrTimer();
        }
    }

    private void StartOcrTimer()
    {
        StopOcrTimer();
        _ocrTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(4) };
        _ocrTimer.Tick += async (_, _) =>
        {
            try
            {
                if (_ocrAll == null)
                {
                    IsOcrModelDownloading = true;
                    _ocrDownloadCts = new CancellationTokenSource();
                    try
                    {
                        var spec = _settingsHostService.Settings.OcrSettings.ModelSpec;
                        var mirror = _settingsHostService.Settings.OcrSettings.Mirror;
                        var model = await _ocrModelService.EnsureAsync(spec, mirror, _ocrDownloadCts.Token);
                        _ocrAll = new PaddleOcrAll(model, _preferMkldnn ? PaddleDevice.Mkldnn() : PaddleDevice.Openblas())
                        {
                            AllowRotateDetection = false,
                            Enable180Classification = false
                        };
                    }
                    finally
                    {
                        Application.Current.Dispatcher.Invoke(() => { IsOcrModelDownloading = false; });
                        _ocrDownloadCts?.Dispose();
                        _ocrDownloadCts = null;
                    }
                }

                var bp = _settingsHostService.Settings.BpWindowSettings;

                if (_isOcrRunning) return;
                _isOcrRunning = true;
                if (IsPickRowModeEnabled && bp.PickOcrRowRegions is { Count: 2 })
                {
                    var resSur = await Task.Run(() => RecognizeResult(bp.PickOcrRowRegions[0]));
                    var tokensSur = ExtractRowTokens(resSur, 4);
                    for (var i = 0; i < tokensSur.Count; i++)
                    {
                        var ch = FindBestCharacterFuzzy(tokensSur[i], _sharedDataService.SurCharaList);
                        if (ch != null && i < _sharedDataService.CurrentGame.SurPlayerList.Count)
                            _sharedDataService.CurrentGame.SurPlayerList[i].Character = ch;
                    }

                    var resHun = await Task.Run(() => RecognizeResult(bp.PickOcrRowRegions[1]));
                    var tokensHun = ExtractRowTokens(resHun, 4);
                    if (tokensHun.Count > 0)
                    {
                        var chHun = FindBestCharacterFuzzy(tokensHun[0], _sharedDataService.HunCharaList);
                        if (chHun != null)
                            _sharedDataService.CurrentGame.HunPlayer.Character = chHun;
                    }
                }

                if (IsBanRowModeEnabled)
                {
                    if (bp.BanSurOcrRowRegion.Width > 0 && bp.BanSurOcrRowRegion.Height > 0)
                    {
                        var resBanSur = await Task.Run(() => RecognizeResult(bp.BanSurOcrRowRegion));
                        var tokensBanSur = ExtractRowTokens(resBanSur, AppConstants.CurrentBanSurCount);
                        var idx = 0;
                        foreach (var tk in tokensBanSur)
                        {
                            if (idx >= AppConstants.CurrentBanSurCount) break;
                            var ch = FindBestCharacterFuzzy(tk, _sharedDataService.SurCharaList);
                            if (ch != null)
                            {
                                _sharedDataService.CurrentGame.CurrentSurBannedList[idx] = ch;
                                idx++;
                            }
                        }
                    }

                    if (bp.BanHunOcrRowRegion.Width > 0 && bp.BanHunOcrRowRegion.Height > 0)
                    {
                        var resBanHun = await Task.Run(() => RecognizeResult(bp.BanHunOcrRowRegion));
                        var tokensBanHun = ExtractRowTokens(resBanHun, AppConstants.CurrentBanHunCount);
                        var idx = 0;
                        foreach (var tk in tokensBanHun)
                        {
                            if (idx >= AppConstants.CurrentBanHunCount) break;
                            var ch = FindBestCharacterFuzzy(tk, _sharedDataService.HunCharaList);
                            if (ch != null)
                            {
                                _sharedDataService.CurrentGame.CurrentHunBannedList[idx] = ch;
                                idx++;
                            }
                        }
                    }
                }
                if (_ocrRunCounter >= OcrRecycleThreshold)
                {
                    _ocrAll?.Dispose();
                    _ocrAll = null;
                    _ocrRunCounter = 0;
                    CompactGc();
                }
                _isOcrRunning = false;
            }
            catch
            {
                _isOcrRunning = false;
            }
        };
        _ocrTimer.Start();
    }

    private static void CompactGc()
    {
        System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, false, true);
    }

    private void StopOcrTimer()
    {
        _ocrTimer?.Stop();
        _ocrTimer = null;
        _ocrDownloadCts?.Cancel();
        _ocrDownloadCts?.Dispose();
        _ocrDownloadCts = null;
    }

    private PaddleOcrResult? RecognizeResult(Int32Rect rect)
    {
        if (_ocrAll == null) return null;
        using var bmp = Capture(rect);
        using var mat = bmp.ToMat();
        var use = EnsureMatSize(mat);
        try
        {
            var r = _ocrAll.Run(use);
            _ocrRunCounter++;
            return r;
        }
        catch
        {
            _preferMkldnn = false;
            try { _ocrAll?.Dispose(); } catch { }
            _ocrAll = null;
            return null;
        }
        finally
        {
            if (!ReferenceEquals(use, mat)) use.Dispose();
        }
    }

    private static System.Drawing.Bitmap Capture(Int32Rect rect)
    {
        using var bmp = new System.Drawing.Bitmap(rect.Width, rect.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(rect.X, rect.Y, 0, 0, new System.Drawing.Size(rect.Width, rect.Height));
        }
        return (System.Drawing.Bitmap)bmp.Clone();
    }

    private static List<string> ExtractRowTokens(PaddleOcrResult? result, int maxCount)
    {
        var list = new List<string>();
        var regions = result?.Regions;
        if (regions != null && regions.Any())
        {
            foreach (var r in regions.OrderBy(r => r.Rect.Center.X))
            {
                if (!string.IsNullOrWhiteSpace(r.Text)) list.Add(r.Text.Trim());
            }
        }
        if (list.Count == 0 && result != null && !string.IsNullOrWhiteSpace(result.Text))
        {
            var t = result.Text.Replace("\r", " ").Replace("\n", " ");
            list = t.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        }
        if (list.Count > maxCount) list = list.Take(maxCount).ToList();
        return list;
    }

    private static Mat EnsureMatSize(Mat src)
    {
        var maxPixels = 2000000;
        var pixels = src.Rows * src.Cols;
        if (pixels <= maxPixels) return src;
        var scale = Math.Sqrt((double)maxPixels / pixels);
        var dst = new Mat();
        Cv2.Resize(src, dst, new OpenCvSharp.Size((int)(src.Cols * scale), (int)(src.Rows * scale)), 0, 0, InterpolationFlags.Area);
        return dst;
    }

    private static string Normalize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        var t = s.Replace("\r", " ").Replace("\n", " ");
        t = new string(t.Where(ch => !char.IsWhiteSpace(ch) && !char.IsPunctuation(ch)).ToArray());
        return t.ToLowerInvariant();
    }

    private static bool ContainsLoose(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
        return a.Contains(b, StringComparison.OrdinalIgnoreCase) || b.Contains(a, StringComparison.OrdinalIgnoreCase);
    }

    private static double Similarity(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0.0;
        if (a == b) return 1.0;
        var dist = LevenshteinDistance(a, b);
        var maxLen = Math.Max(a.Length, b.Length);
        return maxLen == 0 ? 0.0 : 1.0 - (double)dist / maxLen;
    }

    private static int LevenshteinDistance(string s, string t)
    {
        var n = s.Length;
        var m = t.Length;
        var d = new int[n + 1, m + 1];
        for (var i = 0; i <= n; i++) d[i, 0] = i;
        for (var j = 0; j <= m; j++) d[0, j] = j;
        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                var cost = s[i - 1] == t[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }
        return d[n, m];
    }

    private Character? FindBestCharacterFuzzy(string text, Dictionary<string, Character> dict)
    {
        var t = Normalize(text);
        Character? best = null;
        double bestScore = 0.0;
        foreach (var kv in dict)
        {
            var key = Normalize(kv.Key);
            var name = Normalize(kv.Value.Name ?? string.Empty);
            var s1 = Similarity(t, key);
            var s2 = string.IsNullOrEmpty(name) ? 0.0 : Similarity(t, name);
            var s = Math.Max(s1, s2);
            if (ContainsLoose(t, key) || (!string.IsNullOrEmpty(name) && ContainsLoose(t, name)))
                s = Math.Max(s, 0.95);
            if (s > bestScore)
            {
                bestScore = s;
                best = kv.Value;
            }
        }
        return bestScore >= 0.5 ? best : null;
    }

    public void Dispose()
    {
        StopOcrTimer();
        _ocrAll?.Dispose();
        _ocrAll = null;
    }

    ~OcrHelperPageViewModel()
    {
        Dispose();
    }
}
