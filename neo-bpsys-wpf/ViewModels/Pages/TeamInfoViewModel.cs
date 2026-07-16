using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Data;
using Wpf.Ui.Controls;
using Member = neo_bpsys_wpf.Core.Models.Member;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;
using Image = System.Windows.Controls.Image;
using Orientation = System.Windows.Controls.Orientation;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using WrapPanel = System.Windows.Controls.WrapPanel;
using Team = neo_bpsys_wpf.Core.Models.Team;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class TeamInfoPageViewModel
{
    /// <summary>
    /// 队伍信息视图模型，管理单个队伍的名称、颜色、Logo 和成员信息。
    /// </summary>
    public partial class TeamInfoViewModel : ViewModelBase
    {
#pragma warning disable CS8618 
        /// <summary>
        /// 用于设计时预览的无参构造函数。
        /// </summary>
        public TeamInfoViewModel()
#pragma warning restore CS8618 
        {
            // Decorative constructor for design-time only.
        }

        /// <summary>
        /// 获取当前管理的队伍数据。
        /// </summary>
        public Team CurrentTeam { get; private set; }
        private readonly IFilePickerService _filePickerService;
        private readonly IFrontedImageSafetyService _imageSafetyService;
        private readonly ITutorialSignalService _tutorialSignalService;
        private readonly IContentDialogService? _contentDialogService;

        /// <summary>
        /// 初始化队伍信息视图模型。
        /// </summary>
        /// <param name="team">队伍数据</param>
        /// <param name="filePickerService">文件选择服务</param>
        /// <param name="imageSafetyService">前台图片安全校验服务</param>
        public TeamInfoViewModel(
            Team team,
            IFilePickerService filePickerService,
            IFrontedImageSafetyService imageSafetyService)
            : this(team, filePickerService, imageSafetyService, NoOpTutorialSignalService.Instance)
        {
        }

        /// <summary>
        /// 初始化队伍信息视图模型。
        /// </summary>
        /// <param name="team">队伍数据</param>
        /// <param name="filePickerService">文件选择服务</param>
        /// <param name="imageSafetyService">前台图片安全校验服务</param>
        /// <param name="tutorialSignalService">教程信号服务</param>
        public TeamInfoViewModel(
            Team team,
            IFilePickerService filePickerService,
            IFrontedImageSafetyService imageSafetyService,
            ITutorialSignalService tutorialSignalService)
            : this(team, filePickerService, imageSafetyService, tutorialSignalService, null)
        {
        }

        /// <summary>
        /// 初始化队伍信息视图模型。
        /// </summary>
        /// <param name="team">队伍数据</param>
        /// <param name="filePickerService">文件选择服务</param>
        /// <param name="imageSafetyService">前台图片安全校验服务</param>
        /// <param name="tutorialSignalService">教程信号服务</param>
        /// <param name="contentDialogService">内容对话框服务。</param>
        public TeamInfoViewModel(
            Team team,
            IFilePickerService filePickerService,
            IFrontedImageSafetyService imageSafetyService,
            ITutorialSignalService tutorialSignalService,
            IContentDialogService? contentDialogService)
        {
            CurrentTeam = team;
            _filePickerService = filePickerService;
            _imageSafetyService = imageSafetyService;
            _tutorialSignalService = tutorialSignalService;
            _contentDialogService = contentDialogService;
            TeamName = team.Name;
            SyncTeamColorEditor();
            CurrentTeam.PropertyChanged += CurrentTeamOnPropertyChanged;
        }

        [ObservableProperty]
        public partial string TeamName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string TeamColorHexEditText { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string TeamColorStatus { get; set; } = string.Empty;

        private Color _teamColorPickerValue = Colors.White;
        private bool _syncingTeamColorEditor;

        /// <summary>
        /// 获取或设置队伍颜色选择器的当前颜色值。
        /// </summary>
        public Color TeamColorPickerValue
        {
            get => _teamColorPickerValue;
            set
            {
                if (!SetProperty(ref _teamColorPickerValue, value) || _syncingTeamColorEditor)
                    return;

                CurrentTeam.ColorHex = value.ToArgbHexString();
                SyncTeamColorEditor();
            }
        }

        [RelayCommand]
        private void ConfirmTeamName()
        {
            CurrentTeam.Name = TeamName;
            _tutorialSignalService.Publish(TutorialSignalIds.TeamNameConfirmed, CreateTeamPayload());
        }

        [RelayCommand]
        private void ApplyTeamColor()
        {
            if (!ColorHelper.TryNormalizeHex(TeamColorHexEditText, out var normalized))
            {
                TeamColorStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.Team, "InvalidTeamColorHex");
                return;
            }

            CurrentTeam.ColorHex = normalized;
            SyncTeamColorEditor();
        }

        [RelayCommand]
        private void ResetTeamColor()
        {
            var defaultColor = CurrentTeam.TeamType == Core.Enums.TeamType.AwayTeam
                ? Team.DefaultAwayColorHex
                : Team.DefaultHomeColorHex;
            CurrentTeam.ColorHex = defaultColor;
            SyncTeamColorEditor();
        }

        private void CurrentTeamOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Team.ColorHex))
                SyncTeamColorEditor();
        }

        private void SyncTeamColorEditor()
        {
            _syncingTeamColorEditor = true;
            TeamColorHexEditText = CurrentTeam.ColorHex;
            _teamColorPickerValue = ColorHelper.ParseColorOrDefault(CurrentTeam.ColorHex, Colors.White);
            OnPropertyChanged(nameof(TeamColorPickerValue));
            TeamColorStatus = string.Empty;
            _syncingTeamColorEditor = false;
        }

        [RelayCommand]
        private void SetTeamLogo()
        {
            var fileName = _filePickerService.PickImage();

            if (string.IsNullOrEmpty(fileName))
                return;
            try
            {
                var validation = _imageSafetyService.ValidateFile(fileName, FrontedImagePurpose.UiElement);
                if (!validation.IsValid)
                {
                    _ = MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString(AppI18nDictionaries.Team, "LogoFileIsNotValid"));
                    return;
                }

                CurrentTeam.Logo = new BitmapImage(new Uri(fileName));
            }
            catch
            {
                _ = MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString(AppI18nDictionaries.Team, "ImageMaybeDamagedOrUnsupported"));
            }
        }

        [RelayCommand]
        private void ImportInfoFromJson()
        {
            var fileName = _filePickerService.PickJsonFile();

            if (string.IsNullOrEmpty(fileName))
                return;

            var jsonFile = File.ReadAllText(fileName);

            if (string.IsNullOrEmpty(jsonFile))
                return;

            try
            {
                var missingColor = IsTeamColorMissing(jsonFile);
                var teamInfo = JsonSerializer.Deserialize<Team>(jsonFile);

                if (teamInfo == null)
                    return;

                if (missingColor)
                    teamInfo.ColorHex = CurrentTeam.TeamType == Core.Enums.TeamType.HomeTeam
                        ? Team.DefaultHomeColorHex
                        : Team.DefaultAwayColorHex;

                teamInfo.Camp = CurrentTeam.Camp;
                CurrentTeam.ImportTeamInfo(teamInfo);
                TeamName = CurrentTeam.Name;
                RefreshCanMemberOnFieldState(Camp.Sur);
                RefreshCanMemberOnFieldState(Camp.Hun);
                _tutorialSignalService.Publish(GetTeamJsonImportedSignalId(), CreateTeamPayload());
            }
            catch (JsonException ex)
            {
                _ = MessageBoxHelper.ShowErrorAsync(
                    $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Team, "JsonFileFormatError")}\n{ex.Message}");
            }
            catch
            {
                _ = MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString(AppI18nDictionaries.Team, "ImageMaybeDamagedOrUnsupported"));
            }
        }

        private static bool IsTeamColorMissing(string json)
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return true;

            var colorProperty = document.RootElement
                .EnumerateObject()
                .FirstOrDefault(property => string.Equals(property.Name, nameof(Team.ColorHex), StringComparison.OrdinalIgnoreCase));

            return colorProperty.Value.ValueKind switch
            {
                JsonValueKind.Undefined or JsonValueKind.Null => true,
                JsonValueKind.String => string.IsNullOrWhiteSpace(colorProperty.Value.GetString()),
                _ => false
            };
        }

        [RelayCommand]
        private void AddSurMember()
        {
            CurrentTeam.SurMemberList.Add(new Member(Camp.Sur));
            RemoveSurMemberCommand.NotifyCanExecuteChanged();
            RefreshCanMemberOnFieldState(Camp.Sur);
        }

        [RelayCommand(CanExecute = nameof(CanRemoveSurMember))]
        private async Task RemoveSurMemberAsync(Member member)
        {
            await RemoveMemberAsync(member);
        }

        private bool CanRemoveSurMember(Member member) => CurrentTeam.SurMemberList.Count > 4;

        [RelayCommand]
        private void AddHunMember()
        {
            CurrentTeam.HunMemberList.Add(new Member(Camp.Hun));
            RefreshCanMemberOnFieldState(Camp.Hun);
        }

        [RelayCommand(CanExecute = nameof(CanRemoveHunMember))]
        private async Task RemoveHunMemberAsync(Member member)
        {
            await RemoveMemberAsync(member);
        }

        private bool CanRemoveHunMember() => CurrentTeam.HunMemberList.Count > 1;

        private async Task RemoveMemberAsync(Member member)
        {
            var memberName = string.IsNullOrEmpty(member.Name)
                ? string.Empty
                : $" \"{member.Name}\" ";

            var messageBox = new MessageBox()
            {
                Title = I18nHelper.GetLocalizedString(AppI18nDictionaries.Team, "DeleteConfirmation"),
                Content = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Team, "AreYouSureToDelete")} {memberName}？",
                PrimaryButtonText = I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Confirm"),
                PrimaryButtonIcon = new SymbolIcon() { Symbol = SymbolRegular.Delete24 },
                CloseButtonIcon = new SymbolIcon() { Symbol = SymbolRegular.Prohibited20 },
                CloseButtonText = I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Cancel")
            };
            var result = await messageBox.ShowDialogAsync();

            if (result == MessageBoxResult.Primary)
            {
                CurrentTeam.MemberOffField(member);
                if (member.Camp == Camp.Sur)
                {
                    CurrentTeam.SurMemberList.Remove(member);
                }
                else
                {
                    CurrentTeam.HunMemberList.Remove(member);
                }
                RefreshCanMemberOnFieldState(member.Camp);
            }
        }


        [RelayCommand]
        private void SwitchMemberState(Member member)
        {
            if (member.IsOnField)
            {
                member.IsOnField = CurrentTeam.MemberOnField(member);
            }
            else
            {
                CurrentTeam.MemberOffField(member);
            }
            RefreshCanMemberOnFieldState(member.Camp);
            _tutorialSignalService.Publish(
                TutorialSignalIds.MemberStateChanged,
                new
                {
                    CurrentTeam.TeamType,
                    CurrentTeam.Camp,
                    MemberCamp = member.Camp,
                    member.IsOnField,
                    member.Name
                });
        }

        [RelayCommand]
        private async Task EditMemberDetailsAsync(Member member)
        {
            ArgumentNullException.ThrowIfNull(member);

            var contentDialogService = _contentDialogService
                ?? throw new InvalidOperationException("The content dialog service is unavailable.");
            var gameIdTextBox = new TextBox
            {
                PlaceholderText = I18nHelper.GetLocalizedString(AppI18nDictionaries.Team, "InGameName"),
                Text = member.GameId,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            var photoPreview = new Image
            {
                Width = 120,
                Height = 120,
                Source = member.Image,
                Stretch = Stretch.Uniform
            };
            photoPreview.SetBinding(
                Image.SourceProperty,
                new Binding(nameof(Member.Image)) { Source = member });
            var photoActions = new WrapPanel
            {
                Margin = new System.Windows.Thickness(0, 0, 0, 8)
            };
            var setPhotoButton = new Button
            {
                Margin = new System.Windows.Thickness(0, 0, 8, 0),
                Command = SetMemberImageCommand,
                CommandParameter = member,
                Icon = new SymbolIcon(SymbolRegular.ImageAdd24)
            };
            void RefreshPhotoActions()
            {
                setPhotoButton.Content = I18nHelper.GetLocalizedString(
                    AppI18nDictionaries.Team,
                    member.IsImageValid ? "ChangePhoto" : "SetPhoto");
                ClearMemberImageCommand.NotifyCanExecuteChanged();
            }

            RefreshPhotoActions();
            photoActions.Children.Add(setPhotoButton);
            photoActions.Children.Add(new Button
            {
                Command = ClearMemberImageCommand,
                CommandParameter = member,
                Content = I18nHelper.GetLocalizedString(AppI18nDictionaries.Team, "RemovePhoto"),
                Icon = new SymbolIcon(SymbolRegular.ImageOff24)
            });
            var photoPreviewPanel = new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Margin = new System.Windows.Thickness(0, 0, 0, 4),
                        Text = I18nHelper.GetLocalizedString(AppI18nDictionaries.Team, "PhotoPreview")
                    },
                    photoPreview
                }
            };

            var dialog = new ContentDialog
            {
                Title = I18nHelper.GetLocalizedString(AppI18nDictionaries.Team, "PlayerDetails"),
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        new StackPanel
                        {
                            Width = 220,
                            VerticalAlignment = System.Windows.VerticalAlignment.Top,
                            Children = { gameIdTextBox }
                        },
                        new StackPanel
                        {
                            Margin = new System.Windows.Thickness(16, 0, 0, 0),
                            Children = { photoActions, photoPreviewPanel }
                        }
                    }
                },
                PrimaryButtonText = I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Confirm"),
                PrimaryButtonIcon = new SymbolIcon(SymbolRegular.Checkmark24),
                CloseButtonText = I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Cancel"),
                CloseButtonIcon = new SymbolIcon(SymbolRegular.Dismiss24)
            };
            PropertyChangedEventHandler memberPropertyChanged = (_, e) =>
            {
                if (e.PropertyName == nameof(Member.IsImageValid))
                    RefreshPhotoActions();
            };
            member.PropertyChanged += memberPropertyChanged;
            try
            {
                if (await contentDialogService.ShowAsync(dialog) is ContentDialogResult.Primary)
                    member.GameId = gameIdTextBox.Text;
            }
            finally
            {
                member.PropertyChanged -= memberPropertyChanged;
            }
        }

        private object CreateTeamPayload() => new
        {
            CurrentTeam.TeamType,
            CurrentTeam.Camp,
            CurrentTeam.Name
        };

        private string GetTeamJsonImportedSignalId() =>
            CurrentTeam.TeamType == Core.Enums.TeamType.HomeTeam
                ? TutorialSignalIds.TeamJsonImportedHome
                : TutorialSignalIds.TeamJsonImportedAway;

        private sealed class NoOpTutorialSignalService : ITutorialSignalService
        {
            public static NoOpTutorialSignalService Instance { get; } = new();

            public void Publish(string signalId, object? payload = null)
            {
            }

            public Task<object?> WaitAsync(
                string signalId,
                Func<object?, bool>? predicate,
                TimeSpan timeout,
                CancellationToken cancellationToken) =>
                Task.FromResult<object?>(null);
        }

        private void RefreshCanMemberOnFieldState(Camp camp)
        {
            var canOthersOnField = CurrentTeam.CanMemberOnField(camp);
            if (camp == Camp.Sur)
            {
                foreach (var m in CurrentTeam.SurMemberList)
                {
                    if (!m.IsOnField)
                        m.CanOnFieldChange = canOthersOnField;
                }
            }
            else
            {
                foreach (var m in CurrentTeam.HunMemberList)
                {
                    if (!m.IsOnField)
                        m.CanOnFieldChange = canOthersOnField;
                }
            }
            RemoveSurMemberCommand.NotifyCanExecuteChanged();
            RemoveHunMemberCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private void SetMemberImage(Member member)
        {
            var imagePath = _filePickerService.PickImage();
            if (imagePath == null)
                return;

            try
            {
                member.Image = new BitmapImage(new Uri(imagePath));
                ClearMemberImageCommand.NotifyCanExecuteChanged();
            }
            catch
            {
                _ = MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString(AppI18nDictionaries.Team, "ImageMaybeDamagedOrUnsupported"));
            }
        }

        [RelayCommand(CanExecute = nameof(CanClearMemberImage))]
        private async Task ClearMemberImageAsync(Member member)
        {
            if (await MessageBoxHelper.ShowConfirmAsync(I18nHelper.GetLocalizedString(AppI18nDictionaries.Team, "AreYouSureToRemoveTheFileLookPhoto"), I18nHelper.GetLocalizedString(AppI18nDictionaries.Team, "ClearTip"), I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Confirm"), I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Cancel")))
            {
                member.Image = null;
                ClearMemberImageCommand.NotifyCanExecuteChanged();
            }
        }

        private static bool CanClearMemberImage(Member? member) => member?.IsImageValid == true;
    }
}
