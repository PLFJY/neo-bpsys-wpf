using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Helpers;
using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;
using Wpf.Ui.Controls;
using Member = neo_bpsys_wpf.Core.Models.Member;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;
using Team = neo_bpsys_wpf.Core.Models.Team;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class TeamInfoPageViewModel
{
    public partial class TeamInfoViewModel : ViewModelBase
    {
#pragma warning disable CS8618 
        public TeamInfoViewModel()
#pragma warning restore CS8618 
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        public Team CurrentTeam { get; private set; }
        private readonly IFilePickerService _filePickerService;

        public TeamInfoViewModel(Team team, IFilePickerService filePickerService)
        {
            CurrentTeam = team;
            _filePickerService = filePickerService;
        }

        [ObservableProperty]
        private string _teamName = string.Empty;

        [RelayCommand]
        private void ConfirmTeamName()
        {
            CurrentTeam.Name = TeamName;
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