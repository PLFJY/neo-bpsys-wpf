using AutoMapper;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Models;
using neo_bpsys_wpf.Services;
using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    public partial class TeamInfoPageViewModel
    {
        public partial class TeamInfoViewModel : ObservableObject
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
                    CurrentTeam.SurMemberList.Remove(member);
                    RemoveSurMemberCommand.NotifyCanExecuteChanged();
                }
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
                    CurrentTeam.HunMemberList.Remove(member);
                    RemoveHunMemberCommand.NotifyCanExecuteChanged();
                }
            }

            private bool CanRemoveHunMember() => CurrentTeam.HunMemberList.Count > 1;

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
