using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Abstractions.ViewModels;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Messages;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CharaSelectViewModelBase = neo_bpsys_wpf.Core.Abstractions.ViewModels.CharaSelectViewModelBase;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models;
using Sdcb.PaddleInference;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Windows;
using System.Windows.Threading;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class BanHunPageViewModel : ViewModelBase, IDisposable
{
    public BanHunPageViewModel()
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    private readonly ISharedDataService _sharedDataService;
    private readonly ISettingsHostService? _settingsHostService;
    private readonly IOcrModelService? _ocrModelService;
    private DispatcherTimer? _ocrTimer;
    private PaddleOcrAll? _ocrAll;
    private bool _isOcrRunning;
    private bool _preferMkldnn = true;
    private int _ocrRunCounter;
    private const int OcrRecycleThreshold = 60;
    [ObservableProperty] private bool _isBanOcrRecognizing;
    [ObservableProperty] private bool _isOcrModelDownloading;
    private CancellationTokenSource? _ocrDownloadCts;

    public ObservableCollection<bool> CanCurrentHunBanned => _sharedDataService.CanCurrentHunBannedList;

    public BanHunPageViewModel(ISharedDataService sharedDataService)
    {
        _sharedDataService = sharedDataService;
        BanHunCurrentViewModelList = [.. Enumerable.Range(0, AppConstants.CurrentBanHunCount).Select(i => new BanHunCurrentViewModel(_sharedDataService, i))];
        BanHunGlobalViewModelList = [.. Enumerable.Range(0, AppConstants.GlobalBanHunCount).Select(i => new BanHunGlobalViewModel(_sharedDataService, i))];
        sharedDataService.TeamSwapped += OnTeamSwapped;
    }

    public BanHunPageViewModel(ISharedDataService sharedDataService, ISettingsHostService settingsHostService, IOcrModelService ocrModelService)
    {
        _sharedDataService = sharedDataService;
        _settingsHostService = settingsHostService;
        _ocrModelService = ocrModelService;
        BanHunCurrentViewModelList = [.. Enumerable.Range(0, AppConstants.CurrentBanHunCount).Select(i => new BanHunCurrentViewModel(_sharedDataService, i))];
        BanHunGlobalViewModelList = [.. Enumerable.Range(0, AppConstants.GlobalBanHunCount).Select(i => new BanHunGlobalViewModel(_sharedDataService, i))];
        sharedDataService.TeamSwapped += OnTeamSwapped;
    }

    public ObservableCollection<BanHunCurrentViewModel> BanHunCurrentViewModelList { get; set; }
    public ObservableCollection<BanHunGlobalViewModel> BanHunGlobalViewModelList { get; set; }

    [RelayCommand]
    private void SelectBanOcrRowRegion()
    {
        var labels = new[] { "框选当局Ban（监管者）一排" };
        var win = new neo_bpsys_wpf.Views.Windows.RegionSelectorWindow(labels)
        {
            Owner = Application.Current.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        win.ShowDialog();
        var regions = win.Regions;
        if (regions.Count >= 1 && _settingsHostService != null)
        {
            _settingsHostService.Settings.BpWindowSettings.BanOcrRowRegion = regions[0];
            IsBanOcrRecognizing = true;
        }
    }

    partial void OnIsBanOcrRecognizingChanged(bool value)
    {
        if (value) StartOcrTimer(); else StopOcrTimer();
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
                    if (_ocrModelService == null || _settingsHostService == null) return;
                    IsOcrModelDownloading = true;
                    _ocrDownloadCts = new CancellationTokenSource();
                    try
                    {
                        var spec = _settingsHostService.Settings.OcrSettings.ModelSpec;
                        var mirror = _settingsHostService.Settings.OcrSettings.Mirror;
                        var model = await _ocrModelService.EnsureAsync(spec, mirror, _ocrDownloadCts.Token);
                        _ocrAll = new PaddleOcrAll(model, _preferMkldnn ? PaddleDevice.Mkldnn() : PaddleDevice.Blas())
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

                if (_settingsHostService == null) return;
                var bp = _settingsHostService.Settings.BpWindowSettings;
                var rect = bp.BanOcrRowRegion;
                if (rect.Width <= 0 || rect.Height <= 0) return;

                if (_isOcrRunning) return;
                _isOcrRunning = true;

                var resBan = await Task.Run(() => RecognizeResult(rect));
                var tokensBan = ExtractRowTokens(resBan);
                var maxCount = AppConstants.CurrentBanHunCount;
                var idx = 0;
                foreach (var tk in tokensBan)
                {
                    if (idx >= maxCount) break;
                    var ch = FindBestCharacterFuzzy(tk, _sharedDataService.HunCharaList);
                    if (ch != null)
                    {
                        _sharedDataService.CurrentGame.CurrentHunBannedList[idx] = ch;
                        idx++;
                    }
                }
                if (_ocrRunCounter >= OcrRecycleThreshold)
                {
                    _ocrAll?.Dispose();
                    _ocrAll = null;
                    _ocrRunCounter = 0;
                    CompactGc();
                }
            }
            finally
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
        using var screenBmp = new System.Drawing.Bitmap(rect.Width, rect.Height);
        using var g = System.Drawing.Graphics.FromImage(screenBmp);
        g.CopyFromScreen(rect.X, rect.Y, 0, 0, new System.Drawing.Size(rect.Width, rect.Height));
        return (System.Drawing.Bitmap)screenBmp.Clone();
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

    private void OnTeamSwapped(object? sender, EventArgs e)
    {
        for (var i = 0; i < _sharedDataService.CurrentGame.HunTeam.GlobalBannedHunRecordArray.Length; i++)
        {
            BanHunGlobalViewModelList[i].SelectedChara = _sharedDataService.CurrentGame.HunTeam.GlobalBannedHunRecordArray[i];
            BanHunGlobalViewModelList[i].SyncCharaAsync();
        }
    }

    public void Dispose()
    {
        StopOcrTimer();
        _ocrAll?.Dispose();
        _ocrAll = null;
        if (_sharedDataService != null)
        {
            _sharedDataService.TeamSwapped -= OnTeamSwapped;
        }
        if (BanHunCurrentViewModelList != null)
        {
            foreach (var vm in BanHunCurrentViewModelList)
            {
                vm.Dispose();
            }
        }
        if (BanHunGlobalViewModelList != null)
        {
            foreach (var vm in BanHunGlobalViewModelList)
            {
                vm.Dispose();
            }
        }
    }

    ~BanHunPageViewModel()
    {
        Dispose();
    }

    private List<string> ExtractRowTokens(PaddleOcrResult? result)
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
        if (list.Count > 2) list = list.Take(2).ToList();
        return list;
    }

    private neo_bpsys_wpf.Core.Models.Character? FindBestCharacterFuzzy(string text, Dictionary<string, neo_bpsys_wpf.Core.Models.Character> dict)
    {
        var t = Normalize(text);
        neo_bpsys_wpf.Core.Models.Character? best = null;
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

    private static string Normalize(string s)
    {
        s = s.Trim();
        s = s.Replace(" ", string.Empty).Replace("·", string.Empty).Replace(".", string.Empty);
        return s;
    }

    private static bool ContainsLoose(string a, string b)
    {
        a = Normalize(a);
        b = Normalize(b);
        return a.Contains(b) || b.Contains(a);
    }

    private static double Similarity(string a, string b)
    {
        var dist = LevenshteinDistance(a, b);
        var maxLen = Math.Max(a.Length, b.Length);
        return maxLen == 0 ? 1.0 : 1.0 - (double)dist / maxLen;
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

    //基于模板基类的VM实现
    public class BanHunCurrentViewModel : CharaSelectViewModelBase, IDisposable
    {
        public BanHunCurrentViewModel(ISharedDataService sharedDataService, int index = 0) : base(sharedDataService, index)
        {
            CharaList = sharedDataService.HunCharaList;
            IsEnabled = sharedDataService.CanCurrentHunBannedList[index];
            SharedDataService.BanCountChanged += OnBanCountChanged;
            SharedDataService.CurrentGame.CurrentHunBannedList.CollectionChanged += OnCurrentHunBannedChanged;
            SharedDataService.CurrentGameChanged += (_, _) =>
            {
                SharedDataService.CurrentGame.CurrentHunBannedList.CollectionChanged -= OnCurrentHunBannedChanged;
                SharedDataService.CurrentGame.CurrentHunBannedList.CollectionChanged += OnCurrentHunBannedChanged;
                SelectedChara = SharedDataService.CurrentGame.CurrentHunBannedList[Index];
                PreviewImage = SelectedChara?.HeaderImageSingleColor;
            };
            SelectedChara = SharedDataService.CurrentGame.CurrentHunBannedList[Index];
            PreviewImage = SelectedChara?.HeaderImageSingleColor;
        }

        private void OnBanCountChanged(object? sender, BanCountChangedEventArgs e)
        {
            if (e.BanListName == BanListName.CanCurrentHunBanned)
            {
                IsEnabled = SharedDataService.CanCurrentHunBannedList[Index];
            }
        }

        public override Task SyncCharaAsync()
        {
            SharedDataService.CurrentGame.CurrentHunBannedList[Index] = SelectedChara;
            PreviewImage = SharedDataService.CurrentGame.CurrentHunBannedList[Index]?.HeaderImageSingleColor;
            return Task.CompletedTask;
        }

        protected override void SyncIsEnabled()
        {
            SharedDataService.CanCurrentHunBannedList[Index] = IsEnabled;
        }

        protected override bool IsActionNameCorrect(GameAction? action) => action == GameAction.BanHun;

        private void OnCurrentHunBannedChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Replace) return;
            if (e.NewStartingIndex != Index) return;
            SelectedChara = SharedDataService.CurrentGame.CurrentHunBannedList[Index];
            PreviewImage = SelectedChara?.HeaderImageSingleColor;
        }

        public void Dispose()
        {
            SharedDataService.BanCountChanged -= OnBanCountChanged;
            SharedDataService.CurrentGame.CurrentHunBannedList.CollectionChanged -= OnCurrentHunBannedChanged;
        }
    }

    public class BanHunGlobalViewModel : CharaSelectViewModelBase, IDisposable
    {
        public BanHunGlobalViewModel(ISharedDataService sharedDataService, int index = 0) : base(sharedDataService, index)
        {
            CharaList = sharedDataService.HunCharaList;
            IsEnabled = sharedDataService.CanGlobalHunBannedList[index];
            SharedDataService.BanCountChanged += OnBanCountChanged;
        }
        
        private void OnBanCountChanged(object? sender, BanCountChangedEventArgs e)
        {
            if (e.BanListName == BanListName.CanGlobalHunBanned)
            {
                IsEnabled = SharedDataService.CanGlobalHunBannedList[Index];
            }
        }

        public override Task SyncCharaAsync()
        {
            SharedDataService.CurrentGame.HunTeam.GlobalBannedHunList[Index] = SelectedChara;
            PreviewImage = SharedDataService.CurrentGame.HunTeam.GlobalBannedHunList[Index]?.HeaderImageSingleColor;
            return Task.CompletedTask;
        }

        protected override void SyncIsEnabled()
        {
            SharedDataService.CanGlobalHunBannedList[Index] = IsEnabled;
        }

        protected override bool IsActionNameCorrect(GameAction? action) => false;

        public void Dispose()
        {
            SharedDataService.BanCountChanged -= OnBanCountChanged;
        }
    }
}
