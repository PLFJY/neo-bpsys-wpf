using AutoMapper;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Models;
using neo_bpsys_wpf.Services;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    public partial class TeamInfoPageViewModel
    {
        public partial class TeamInfoViewModel : ObservableRecipient
        {
            public TeamInfoViewModel()
            {
                //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
            }

            public Team CurrentTeam { get; private set; }
            private readonly IFilePickerService _filePickerService;
            private readonly IMessageBoxService _messageBoxService;
            private readonly IMapper _mapper;

            public TeamInfoViewModel(
                Team team,
                IFilePickerService filePickerService,
                IMessageBoxService messageBoxService,
                IMapper mapper
            )
            {
                CurrentTeam = team;
                _filePickerService = filePickerService;
                _messageBoxService = messageBoxService;
                _mapper = mapper;
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

                CurrentTeam.Logo = new BitmapImage(new Uri(fileName));
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

                var teamInfo = JsonSerializer.Deserialize<Team>(jsonFile);

                if (teamInfo == null)
                    return;

                teamInfo.Camp = CurrentTeam.Camp;
                _mapper.Map(teamInfo, CurrentTeam);
                TeamName = CurrentTeam.Name;

                OnPropertyChanged();
            }

            [RelayCommand(CanExecute = nameof(CanAddSurMember))]
            private void AddSurMember()
            {
                CurrentTeam.SurMemberList.Add(new Member(Camp.Sur));
                RemoveSurMemberCommand.NotifyCanExecuteChanged();
            }

            private bool CanAddSurMember() => true;

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
                RemoveHunMemberCommand.NotifyCanExecuteChanged();
            }

            private bool CanAddHunMember() => true;

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

                if (
                    await _messageBoxService.ShowDeleteConfirmAsync(
                        "删除确认",
                        $"确定删除{memberName}吗？"
                    )
                )
                {
                    CurrentTeam.RemoveMemberInPlayer(member);
                    CurrentTeam.SurMemberList.Remove(member);
                    RemoveSurMemberCommand.NotifyCanExecuteChanged();
                }
            }

            private bool CanRemoveHunMember() => CurrentTeam.HunMemberList.Count > 1;

            [RelayCommand]
            private void SwitchMemberState(Member member)
            {
                bool canOthersOnField;

                if (member.IsOnField)
                {
                    member.IsOnField = CurrentTeam.AddMemberInPlayer(member);
                    canOthersOnField = CurrentTeam.CanAddMemberInPlayer(member.Camp);
                }
                else
                {
                    CurrentTeam.RemoveMemberInPlayer(member);
                    canOthersOnField = true;
                }
                WeakReferenceMessenger.Default.Send(new PropertyChangedMessage<Member.CanOnFieldChangedMessageType>
                    (this, nameof(Member.CanOnFieldChange),
                    new Member.CanOnFieldChangedMessageType(member.Camp),
                    new Member.CanOnFieldChangedMessageType(member.Camp, canOthersOnField)));
            }


            [RelayCommand]
            private void SetMemberImage(Member member)
            {
                var imagePath = _filePickerService.PickImage();
                if (imagePath == null)
                    return;

                member.Image = ImageHelper.GetImageFromPath(imagePath);
            }

        }
    }
}
