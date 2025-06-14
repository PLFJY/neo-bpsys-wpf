using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Messages;
using neo_bpsys_wpf.Models;
using neo_bpsys_wpf.Services;
using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    public partial class TeamInfoPageViewModel
    {
        public partial class TeamInfoViewModel : ObservableRecipient
        {
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
            public TeamInfoViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
            {
                //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
            }

            public Team CurrentTeam { get; private set; }
            private readonly IFilePickerService _filePickerService;
            private readonly IMessageBoxService _messageBoxService;

            public TeamInfoViewModel(Team team, IFilePickerService filePickerService, IMessageBoxService messageBoxService)
            {
                CurrentTeam = team;
                _filePickerService = filePickerService;
                _messageBoxService = messageBoxService;
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
                    _messageBoxService.ShowErrorAsync($"图片文件可能损坏或格式不受支持");
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
                    App.Services.GetRequiredService<ISharedDataService>().CurrentGame.RefreshCurrentPlayer();
                    OnPropertyChanged();
                }
                catch (JsonException ex)
                {
                    _messageBoxService.ShowErrorAsync($"Json文件格式错误\n{ex.Message}");
                }
                catch
                {
                    _messageBoxService.ShowErrorAsync($"图片文件可能损坏或格式不受支持");
                }
            }

            [RelayCommand(CanExecute = nameof(CanAddSurMember))]
            private void AddSurMember()
            {
                CurrentTeam.SurMemberList.Add(new Member(Camp.Sur));
                RemoveSurMemberCommand.NotifyCanExecuteChanged();
                RefreshCanMemberOnFieldState(Camp.Sur);
            }

            private static bool CanAddSurMember() => true;

            [RelayCommand(CanExecute = nameof(CanRemoveSurMember))]
            private async Task RemoveSurMemberAsync(Member member)
            {
                await RemoveMemberAsync(member);
            }

            private bool CanRemoveSurMember(Member member) => CurrentTeam.SurMemberList.Count > 4;

            [RelayCommand(CanExecute = nameof(CanAddHunMember))]
            private void AddHunMember()
            {
                CurrentTeam.HunMemberList.Add(new Member(Camp.Hun));
                RefreshCanMemberOnFieldState(Camp.Hun);
            }

            private static bool CanAddHunMember() => true;

            [RelayCommand(CanExecute = nameof(CanRemoveHunMember))]
            private async Task RemoveHunMemberAsync(Member member)
            {
                await RemoveMemberAsync(member);
            }

            private async Task RemoveMemberAsync(Member member)
            {
                var memberName = string.IsNullOrEmpty(member.Name)
                    ? string.Empty
                    : $" \"{member.Name}\" ";

                if (await _messageBoxService.ShowDeleteConfirmAsync("删除确认", $"确定删除{memberName}吗？"))
                {
                    CurrentTeam.RemoveMemberInPlayer(member);
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

            private bool CanRemoveHunMember() => CurrentTeam.HunMemberList.Count > 1;

            [RelayCommand]
            private void SwitchMemberState(Member member)
            {
                if (member.IsOnField)
                {
                    member.IsOnField = CurrentTeam.AddMemberInPlayer(member);
                }
                else
                {
                    CurrentTeam.RemoveMemberInPlayer(member);
                }
                RefreshCanMemberOnFieldState(member.Camp);
            }

            private void RefreshCanMemberOnFieldState(Camp camp)
            {
                var canOthersOnField = CurrentTeam.CanAddMemberInPlayer(camp);
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
                    _messageBoxService.ShowErrorAsync($"图片文件可能损坏或格式不受支持");
                }
            }

            [RelayCommand]
            private static void ClearMamberImage(Member member)
            {
                member.Image = null;
            }
        }
    }
}
