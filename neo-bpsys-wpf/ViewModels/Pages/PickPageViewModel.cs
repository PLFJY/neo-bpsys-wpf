using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Controls;
using neo_bpsys_wpf.Core.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Abstractions.ViewModels;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Messages;
using CharaSelectViewModelBase = neo_bpsys_wpf.Core.Abstractions.ViewModels.CharaSelectViewModelBase;
using Team = neo_bpsys_wpf.Core.Models.Team;
using System.Windows.Media.Imaging;
using neo_bpsys_wpf.Core;
using System.Windows;
using System.Windows.Threading;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models;
using Sdcb.PaddleInference;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class PickPageViewModel : ViewModelBase, IRecipient<HighlightMessage>, IDisposable
{
    public PickPageViewModel()
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    private readonly ISharedDataService _sharedDataService;
    private readonly IFrontService _frontService;
    private readonly ISettingsHostService _settingsHostService;
    private readonly IOcrModelService _ocrModelService;
    private DispatcherTimer? _ocrTimer;
    private PaddleOcrAll? _ocrAll;
    private bool _isOcrRunning;
    private bool _preferMkldnn = true;
    private int _ocrRunCounter;
    private const int OcrRecycleThreshold = 60;
    [ObservableProperty] private bool _isOcrRecognizing;
    [ObservableProperty] private bool _isOcrModelDownloading;
    private CancellationTokenSource? _ocrDownloadCts;
    [ObservableProperty] private bool _isRowModeEnabled;

    public PickPageViewModel(ISharedDataService sharedDataService, IFrontService frontService, ISettingsHostService settingsHostService, IOcrModelService ocrModelService)
    {
        _sharedDataService = sharedDataService;
        _frontService = frontService;
        _settingsHostService = settingsHostService;
        _ocrModelService = ocrModelService;
        SurPickViewModelList =
            [.. Enumerable.Range(0, 4).Select(i => new SurPickViewModel(sharedDataService, frontService, i))];
        HunPickVm = new HunPickViewModel(sharedDataService, frontService);
        MainSurGlobalBanRecordViewModelList =
            [.. Enumerable.Range(0, AppConstants.GlobalBanSurCount).Select(i => new MainSurGlobalBanRecordViewModel(sharedDataService, i))];
        MainHunGlobalBanRecordViewModelList =
            [.. Enumerable.Range(0, AppConstants.GlobalBanHunCount).Select(i => new MainHunGlobalBanRecordViewModel(sharedDataService, i))];
        AwaySurGlobalBanRecordViewModelList =
            [.. Enumerable.Range(0, AppConstants.GlobalBanSurCount).Select(i => new AwaySurGlobalBanRecordViewModel(sharedDataService, i))];
        AwayHunGlobalBanRecordViewModelList =
            [.. Enumerable.Range(0, AppConstants.GlobalBanHunCount).Select(i => new AwayHunGlobalBanRecordViewModel(sharedDataService, i))];
    }

    [RelayCommand]
    private void SelectOcrRegions()
    {
        var win = new neo_bpsys_wpf.Views.Windows.RegionSelectorWindow
        {
            Owner = Application.Current.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        win.ShowDialog();
        var regions = win.Regions;
        if (regions.Count == 5)
        {
            _settingsHostService.Settings.BpWindowSettings.PickOcrRegions = [.. regions];
            if (IsOcrRecognizing) StartOcrTimer();
        }
    }

    [RelayCommand]
    private void SelectOcrRowRegions()
    {
        var labels = new[] { "框选求生者一排", "框选监管者" };
        var win = new neo_bpsys_wpf.Views.Windows.RegionSelectorWindow(labels)
        {
            Owner = Application.Current.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        win.ShowDialog();
        var regions = win.Regions;
        if (regions.Count == 2)
        {
            _settingsHostService.Settings.BpWindowSettings.PickOcrRowRegions = [.. regions];
            _settingsHostService.Settings.BpWindowSettings.PickOcrRowMode = true;
            IsRowModeEnabled = true;
            if (IsOcrRecognizing) StartOcrTimer();
        }
    }

    private void StartOcrTimer()
    {
        _ocrTimer?.Stop();
        _ocrTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(4)
        };
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
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    finally
                    {
                        IsOcrModelDownloading = false;
                    }
                }

                var bp = _settingsHostService.Settings.BpWindowSettings;

                if (_isOcrRunning) return;
                _isOcrRunning = true;

                if (bp.PickOcrRowMode && bp.PickOcrRowRegions is { Count: 2 })
                {
                    var res = await Task.Run(() => RecognizeResult(bp.PickOcrRowRegions[0]));
                    var tokens = ExtractRowTokens(res);
                    for (var i = 0; i < Math.Min(4, tokens.Count); i++)
                    {
                        var ch = FindBestCharacterFuzzy(tokens[i], SurPickViewModelList[i].CharaList);
                        if (ch != null)
                        {
                            SurPickViewModelList[i].SelectedChara = ch;
                            await SurPickViewModelList[i].SyncCharaAsync();
                        }
                    }

                    var hunText = await Task.Run(() => Recognize(bp.PickOcrRowRegions[1]));
                    if (!string.IsNullOrWhiteSpace(hunText))
                    {
                        var chHun = FindBestCharacterFuzzy(hunText, HunPickVm.CharaList);
                        if (chHun != null)
                        {
                            HunPickVm.SelectedChara = chHun;
                            await HunPickVm.SyncCharaAsync();
                        }
                    }
                }
                else
                {
                    var rects = bp.PickOcrRegions;
                    if (rects == null || rects.Count != 5) return;

                    for (var i = 0; i < 4; i++)
                    {
                        var text = await Task.Run(() => Recognize(rects[i]));
                        if (string.IsNullOrWhiteSpace(text)) continue;
                        var ch = FindBestCharacterFuzzy(text, SurPickViewModelList[i].CharaList);
                        if (ch != null)
                        {
                            SurPickViewModelList[i].SelectedChara = ch;
                            await SurPickViewModelList[i].SyncCharaAsync();
                        }
                    }

                    var textHun = await Task.Run(() => Recognize(rects[4]));
                    if (!string.IsNullOrWhiteSpace(textHun))
                    {
                        var ch = FindBestCharacterFuzzy(textHun, HunPickVm.CharaList);
                        if (ch != null)
                        {
                            HunPickVm.SelectedChara = ch;
                            await HunPickVm.SyncCharaAsync();
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

    partial void OnIsOcrRecognizingChanged(bool value)
    {
        if (value)
        {
            var bp = _settingsHostService.Settings.BpWindowSettings;
            if (bp.PickOcrRowMode)
            {
                var rects2 = bp.PickOcrRowRegions;
                if (rects2 == null || rects2.Count != 2) return;
            }
            else
            {
                var rects = bp.PickOcrRegions;
                if (rects == null || rects.Count != 5) return;
            }
            StartOcrTimer();
        }
        else
        {
            StopOcrTimer();
        }
    }

    partial void OnIsRowModeEnabledChanged(bool value)
    {
        _settingsHostService.Settings.BpWindowSettings.PickOcrRowMode = value;
        if (!IsOcrRecognizing) return;
        var bp = _settingsHostService.Settings.BpWindowSettings;
        if (value)
        {
            if (bp.PickOcrRowRegions == null || bp.PickOcrRowRegions.Count != 2)
            {
                SelectOcrRowRegions();
                return;
            }
        }
        else
        {
            if (bp.PickOcrRegions == null || bp.PickOcrRegions.Count != 5)
            {
                SelectOcrRegions();
                return;
            }
        }
        StopOcrTimer();
        StartOcrTimer();
    }

    private string Recognize(Int32Rect rect)
    {
        if (_ocrAll == null || rect.Width <= 0 || rect.Height <= 0) return string.Empty;
        using var bmp = new System.Drawing.Bitmap(rect.Width, rect.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(rect.X, rect.Y, 0, 0, new System.Drawing.Size(rect.Width, rect.Height));
        }
        using var mat = bmp.ToMat();
        var use = EnsureMatSize(mat);
        try
        {
            var result = _ocrAll.Run(use);
            _ocrRunCounter++;
            return result.Text?.Trim() ?? string.Empty;
        }
        catch
        {
            _preferMkldnn = false;
            try { _ocrAll?.Dispose(); } catch { }
            _ocrAll = null;
            return string.Empty;
        }
        finally
        {
            if (!ReferenceEquals(use, mat)) use.Dispose();
        }
    }

    private PaddleOcrResult? RecognizeResult(Int32Rect rect)
    {
        if (_ocrAll == null || rect.Width <= 0 || rect.Height <= 0) return null;
        using var bmp = new System.Drawing.Bitmap(rect.Width, rect.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(rect.X, rect.Y, 0, 0, new System.Drawing.Size(rect.Width, rect.Height));
        }
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
        if (list.Count > 4) list = list.Take(4).ToList();
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

    public void Dispose()
    {
        StopOcrTimer();
        _ocrAll?.Dispose();
        _ocrAll = null;
    }

    ~PickPageViewModel()
    {
        Dispose();
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
        if (n == 0) return m;
        if (m == 0) return n;
        var d = new int[n + 1, m + 1];
        for (int i = 0; i <= n; i++) d[i, 0] = i;
        for (int j = 0; j <= m; j++) d[0, j] = j;
        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                var cost = s[i - 1] == t[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }
        return d[n, m];
    }

    [RelayCommand]
    private async Task PickingBorderSwitchAsync(string arg)
    {
        if (arg == "Hun")
        {
            if (HunPickingBorder)
                await _frontService.BreathingStart(FrontWindowType.BpWindow, "HunPickingBorder", -1, string.Empty);
            else
                await _frontService.BreathingStop(FrontWindowType.BpWindow, "HunPickingBorder", -1, string.Empty);
            return;
        }

        var argsMapSur = new Dictionary<string, int[]>
        {
            { "0", [0] },
            { "1", [1] },
            { "2", [2] },
            { "3", [3] },
            { "0and1", [0, 1] }
        };

        for (var i = 0; i < argsMapSur[arg].Length; i++)
        {
            var index = argsMapSur[arg][i];
            if (SurPickingBorderList[index])
            {
                if (i == argsMapSur[arg].Length - 1)
                {
                    await _frontService.BreathingStart(FrontWindowType.BpWindow, "SurPickingBorder", index,
                        string.Empty);
                }
                else
                {
                    _ = _frontService.BreathingStart(FrontWindowType.BpWindow, "SurPickingBorder", index,
                        string.Empty);
                }
            }
            else
            {
                if (i == argsMapSur[arg].Length - 1)
                {
                    await _frontService.BreathingStop(FrontWindowType.BpWindow, "SurPickingBorder", index,
                        string.Empty);
                }
                else
                {
                    _ = _frontService.BreathingStop(FrontWindowType.BpWindow, "SurPickingBorder", index,
                        string.Empty);
                }
            }
        }
    }

    public void Receive(HighlightMessage message)
    {
        if (message.GameAction == GameAction.PickSur)
        {
            if (message.Index == null) return;
            foreach (var i in message.Index)
            {
                SurPickingBorderList[i] = true;
                _ = PickingBorderSwitchAsync(i.ToString());
            }
        }
        else
        {
            for (var i = 0; i < SurPickingBorderList.Count; i++)
            {
                if (!SurPickingBorderList[i]) continue;
                SurPickingBorderList[i] = false;
                _ = PickingBorderSwitchAsync(i.ToString());
            }
        }

        if (message.GameAction == GameAction.PickHun)
        {
            HunPickingBorder = true;
            _ = PickingBorderSwitchAsync("Hun");
        }
        else
        {
            if (!HunPickingBorder) return;
            HunPickingBorder = false;
            _ = PickingBorderSwitchAsync("Hun");
        }
    }

    public Team MainTeam => _sharedDataService.MainTeam;
    public Team AwayTeam => _sharedDataService.AwayTeam;

    public ObservableCollection<bool> SurPickingBorderList { get; set; } =
        [.. Enumerable.Range(0, 4).Select(_ => false)];

    [ObservableProperty] private bool _hunPickingBorder;

    public ObservableCollection<SurPickViewModel> SurPickViewModelList { get; set; }
    public HunPickViewModel HunPickVm { get; set; }
    public ObservableCollection<MainSurGlobalBanRecordViewModel> MainSurGlobalBanRecordViewModelList { get; set; }
    public ObservableCollection<MainHunGlobalBanRecordViewModel> MainHunGlobalBanRecordViewModelList { get; set; }
    public ObservableCollection<AwaySurGlobalBanRecordViewModel> AwaySurGlobalBanRecordViewModelList { get; set; }
    public ObservableCollection<AwayHunGlobalBanRecordViewModel> AwayHunGlobalBanRecordViewModelList { get; set; }

    //基于模板基类的VM实现
    public partial class SurPickViewModel : CharaSelectViewModelBase
    {
        private readonly IFrontService _frontService;
        public Player ThisPlayer => SharedDataService.CurrentGame.SurPlayerList[Index];

        public SurPickViewModel(ISharedDataService sharedDataService, IFrontService frontService, int index = 0) :
            base(sharedDataService, index)
        {
            _frontService = frontService;
            CharaList = sharedDataService.SurCharaList;
            sharedDataService.CurrentGameChanged += (_, _) =>
            {
                ThisPlayer.PropertyChanged -= OnThisPlayerPropertyChanged;
                OnPropertyChanged(nameof(ThisPlayer));
                ThisPlayer.PropertyChanged += OnThisPlayerPropertyChanged;
            };
            sharedDataService.TeamSwapped += (_, _) => OnPropertyChanged(nameof(ThisPlayer));
            ThisPlayer.PropertyChanged += OnThisPlayerPropertyChanged;
        }

        private void OnThisPlayerPropertyChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(ThisPlayer.Character)) 
                ReverseSyncChara();
        }

        public override async Task SyncCharaAsync()
        {
            _frontService.FadeOutAnimation(FrontWindowType.BpWindow, "SurPick", Index, string.Empty);
            await Task.Delay(250);
            ThisPlayer.Character = SelectedChara;
            _frontService.FadeInAnimation(FrontWindowType.BpWindow, "SurPick", Index, string.Empty);
            PreviewImage = ThisPlayer.Character?.HeaderImage;
        }

        [RelayCommand]
        private async Task SwapCharacterInPlayersAsync(CharacterChangerCommandParameter parameter)
        {
            _frontService.FadeOutAnimation(FrontWindowType.BpWindow, "SurPick", parameter.Source, string.Empty);
            _frontService.FadeOutAnimation(FrontWindowType.BpWindow, "SurPick", parameter.Target, string.Empty);
            await Task.Delay(250);
            SharedDataService.CurrentGame.SwapCharactersInPlayers(parameter.Source, parameter.Target);
            _frontService.FadeInAnimation(FrontWindowType.BpWindow, "SurPick", parameter.Source, string.Empty);
            _frontService.FadeInAnimation(FrontWindowType.BpWindow, "SurPick", parameter.Target, string.Empty);
        }

        private void ReverseSyncChara()
        {
            SelectedChara = SharedDataService.CurrentGame.SurPlayerList[Index].Character;
            PreviewImage = SharedDataService.CurrentGame.SurPlayerList[Index].Character?.HeaderImage;
        }

        protected override void SyncIsEnabled() => throw new NotImplementedException();

        protected override bool IsActionNameCorrect(GameAction? action) => action == GameAction.PickSur;
    }

    public class HunPickViewModel : CharaSelectViewModelBase
    {
        private readonly IFrontService _frontService;

        public HunPickViewModel(ISharedDataService sharedDataService, IFrontService frontService) : base(
            sharedDataService)
        {
            _frontService = frontService;
            CharaList = sharedDataService.HunCharaList;
        }

        public override async Task SyncCharaAsync()
        {
            _frontService.FadeOutAnimation(FrontWindowType.BpWindow, "HunPick", -1, string.Empty);
            await Task.Delay(250);
            SharedDataService.CurrentGame.HunPlayer.Character = SelectedChara;
            _frontService.FadeInAnimation(FrontWindowType.BpWindow, "HunPick", -1, string.Empty);
            PreviewImage = SharedDataService.CurrentGame.HunPlayer.Character?.HeaderImage;
        }

        protected override void SyncIsEnabled()
        {
            throw new NotImplementedException();
        }

        protected override bool IsActionNameCorrect(GameAction? action) => action == GameAction.PickHun;
    }

    public class MainSurGlobalBanRecordViewModel : CharaSelectViewModelBase
    {
        private Character? _recordedChara;

        public Character? RecordedChara
        {
            get => _recordedChara;
            set
            {
                _recordedChara = value;
                SharedDataService.MainTeam.GlobalBannedSurRecordArray[Index] = _recordedChara;
            }
        }

        public MainSurGlobalBanRecordViewModel(ISharedDataService sharedDataService, int index = 0) : base(
            sharedDataService, index)
        {
            CharaList = sharedDataService.SurCharaList;
        }

        public override Task SyncCharaAsync() => throw new NotImplementedException();

        protected override void SyncIsEnabled()
        {
            throw new NotImplementedException();
        }

        protected override bool IsActionNameCorrect(GameAction? action) => false;
    }

    public class MainHunGlobalBanRecordViewModel : CharaSelectViewModelBase
    {
        private Character? _recordedChara;

        public Character? RecordedChara
        {
            get => _recordedChara;
            set
            {
                _recordedChara = value;
                SharedDataService.MainTeam.GlobalBannedHunRecordArray[Index] = _recordedChara;
            }
        }

        public MainHunGlobalBanRecordViewModel(ISharedDataService sharedDataService, int index = 0) : base(
            sharedDataService, index)
        {
            CharaList = sharedDataService.HunCharaList;
        }

        public override Task SyncCharaAsync() => throw new NotImplementedException();

        protected override void SyncIsEnabled() => throw new NotImplementedException();

        protected override bool IsActionNameCorrect(GameAction? action) => false;
    }

    public class AwaySurGlobalBanRecordViewModel : CharaSelectViewModelBase
    {
        private Character? _recordedChara;

        public Character? RecordedChara
        {
            get => _recordedChara;
            set
            {
                _recordedChara = value;
                SharedDataService.AwayTeam.GlobalBannedSurRecordArray[Index] = _recordedChara;
            }
        }

        public AwaySurGlobalBanRecordViewModel(ISharedDataService sharedDataService, int index = 0) : base(
            sharedDataService, index)
        {
            CharaList = sharedDataService.SurCharaList;
        }

        public override Task SyncCharaAsync() => throw new NotImplementedException();

        protected override void SyncIsEnabled() => throw new NotImplementedException();

        protected override bool IsActionNameCorrect(GameAction? action) => false;
    }

    public class AwayHunGlobalBanRecordViewModel : CharaSelectViewModelBase
    {
        private Character? _recordedChara;

        public Character? RecordedChara
        {
            get => _recordedChara;
            set
            {
                _recordedChara = value;
                SharedDataService.AwayTeam.GlobalBannedHunRecordArray[Index] = _recordedChara;
            }
        }

        public AwayHunGlobalBanRecordViewModel(ISharedDataService sharedDataService, int index = 0) : base(
            sharedDataService, index)
        {
            CharaList = sharedDataService.HunCharaList;
        }

        public override Task SyncCharaAsync() => throw new NotImplementedException();

        protected override void SyncIsEnabled() => throw new NotImplementedException();

        protected override bool IsActionNameCorrect(GameAction? action) => false;
    }
}
