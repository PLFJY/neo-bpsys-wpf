using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Helpers;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wpf.Ui.Controls;
using Member = neo_bpsys_wpf.Core.Models.Member;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;
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

        /// <summary>
        /// 初始化队伍信息视图模型。
        /// </summary>
        /// <param name="team">队伍数据</param>
        /// <param name="filePickerService">文件选择服务</param>
        public TeamInfoViewModel(Team team, IFilePickerService filePickerService)
        {
            CurrentTeam = team;
            _filePickerService = filePickerService;
            TeamName = team.Name;
            SyncTeamColorEditor();
            CurrentTeam.PropertyChanged += CurrentTeamOnPropertyChanged;
        }

        [ObservableProperty]
        private string _teamName = string.Empty;

        [ObservableProperty]
        private string _teamColorHexEditText = string.Empty;

        [ObservableProperty]
        private string _teamColorStatus = string.Empty;

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
        }

        [RelayCommand]
        private void ApplyTeamColor()
        {
            if (!ColorHelper.TryNormalizeHex(TeamColorHexEditText, out var normalized))
            {
                TeamColorStatus = I18nHelper.GetLocalizedString("InvalidTeamColorHex");
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
                CurrentTeam.Logo = new BitmapImage(new Uri(fileName));
            }
            catch
            {
                _ = MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString("ImageMaybeDamagedOrUnsupported"));
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
                var teamInfo = JsonSerializer.Deserialize<Team>(jsonFile);

                if (teamInfo == null)
                    return;

                teamInfo.Camp = CurrentTeam.Camp;
                CurrentTeam.ImportTeamInfo(teamInfo);
                TeamName = CurrentTeam.Name;
                RefreshCanMemberOnFieldState(Camp.Sur);
                RefreshCanMemberOnFieldState(Camp.Hun);
            }
            catch (JsonException ex)
            {
                _ = MessageBoxHelper.ShowErrorAsync(
                    $"{I18nHelper.GetLocalizedString("JsonFileFormatError")}\n{ex.Message}");
            }
            catch
            {
                _ = MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString("ImageMaybeDamagedOrUnsurpported"));
            }
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
                Title = I18nHelper.GetLocalizedString("DeleteConfirmation"),
                Content = $"{I18nHelper.GetLocalizedString("AreYouSureToDelete")} {memberName}？",
                PrimaryButtonText = I18nHelper.GetLocalizedString("Confirm"),
                PrimaryButtonIcon = new SymbolIcon() { Symbol = SymbolRegular.Delete24 },
                CloseButtonIcon = new SymbolIcon() { Symbol = SymbolRegular.Prohibited20 },
                CloseButtonText = I18nHelper.GetLocalizedString("Cancel")
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
            }
            catch
            {
                _ = MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString("ImageMaybeDamagedOrUnsurpported"));
            }
        }

        [RelayCommand]
        private async Task ClearMemberImageAsync(Member member)
        {
            if (await MessageBoxHelper.ShowConfirmAsync(I18nHelper.GetLocalizedString("AreYouSureToRemoveTheFileLookPhoto"), I18nHelper.GetLocalizedString("ClearTip"), I18nHelper.GetLocalizedString("Confirm"), I18nHelper.GetLocalizedString("Cancel")))
                member.Image = null;
        }
    }
}
