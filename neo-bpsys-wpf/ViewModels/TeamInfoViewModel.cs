using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Models;
using neo_bpsys_wpf.Services;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    public partial class TeamInfoPageViewModel
    {
        /// <summary>
        /// 战队信息页面视图模型，负责管理战队数据展示和用户交互逻辑
        /// </summary>
        public partial class TeamInfoViewModel : ObservableRecipient
        {
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
            /// <summary>
            /// 设计时构造函数，用于XAML设计器实例化
            /// </summary>
            public TeamInfoViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
            {
                //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
            }

            /// <summary>
            /// 当前操作的战队对象
            /// </summary>
            public Team CurrentTeam { get; private set; }

            /// <summary>
            /// 文件选择服务接口实例
            /// </summary>
            private readonly IFilePickerService _filePickerService;

            /// <summary>
            /// 消息框服务接口实例
            /// </summary>
            private readonly IMessageBoxService _messageBoxService;

            /// <summary>
            /// 主构造函数，用于运行时实例化
            /// </summary>
            /// <param name="team">要操作的战队对象</param>
            /// <param name="filePickerService">文件选择服务</param>
            /// <param name="messageBoxService">消息框服务</param>
            public TeamInfoViewModel(
                Team team,
                IFilePickerService filePickerService,
                IMessageBoxService messageBoxService
            )
            {
                CurrentTeam = team;
                _filePickerService = filePickerService;
                _messageBoxService = messageBoxService;
            }

            /// <summary>
            /// 战队名称绑定属性（支持MVVM双向绑定）
            /// </summary>
            [ObservableProperty]
            private string _teamName = string.Empty;

            /// <summary>
            /// 确认战队名称修改命令执行方法
            /// 将输入框中的名称同步到当前战队对象
            /// </summary>
            [RelayCommand]
            private void ConfirmTeamName()
            {
                CurrentTeam.Name = TeamName;
            }

            /// <summary>
            /// 设置战队Logo命令执行方法
            /// 通过文件选择器获取图片并更新战队Logo
            /// </summary>
            [RelayCommand]
            private void SetTeamLogo()
            {
                var fileName = _filePickerService.PickImage();

                if (string.IsNullOrEmpty(fileName))
                    return;

                CurrentTeam.Logo = new BitmapImage(new Uri(fileName));
            }

            /// <summary>
            /// 从JSON文件导入战队信息命令执行方法
            /// 包含异常处理和数据同步逻辑
            /// </summary>
            /// <returns>异步操作任务</returns>
            [RelayCommand]
            private async Task ImportInfoFromJsonAsync()
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
                    App.Services.GetRequiredService<ISharedDataService>()
                    .CurrentGame.RefreshCurrentPlayer();
                    OnPropertyChanged();
                }
                catch (JsonException ex)
                {
                    await _messageBoxService.ShowWarningAsync($"Json文件格式错误\n{ex.Message}");
                }
            }

            /// <summary>
            /// 添加求生者阵营成员命令执行方法
            /// 添加新成员并更新相关状态
            /// </summary>
            [RelayCommand(CanExecute = nameof(CanAddSurMember))]
            private void AddSurMember()
            {
                CurrentTeam.SurMemberList.Add(new Member(Camp.Sur));
                RemoveSurMemberCommand.NotifyCanExecuteChanged();
                var canOthersOnField = CurrentTeam.CanAddMemberInPlayer(Camp.Sur);
                RefreshCanMemberOnFieldState(Camp.Sur);
            }

            /// <summary>
            /// 判断是否可以添加求生者成员
            /// </summary>
            /// <returns>始终返回true允许添加</returns>
            private bool CanAddSurMember() => true;

            /// <summary>
            /// 移除求生者成员命令执行方法
            /// 显示确认对话框后执行移除操作
            /// </summary>
            /// <param name="member">要移除的成员对象</param>
            /// <returns>异步操作任务</returns>
            [RelayCommand(CanExecute = nameof(CanRemoveSurMember))]
            private async Task RemoveSurMemberAsync(Member member)
            {
                await RemoveMemberAsync(member);
            }

            /// <summary>
            /// 判断是否可以移除求生者成员
            /// </summary>
            /// <param name="member">要移除的成员对象</param>
            /// <returns>当成员数量大于4时返回true</returns>
            private bool CanRemoveSurMember(Member member) => CurrentTeam.SurMemberList.Count > 4;

            /// <summary>
            /// 添加监管者阵营成员命令执行方法
            /// 添加新成员并更新相关状态
            /// </summary>
            [RelayCommand(CanExecute = nameof(CanAddHunMember))]
            private void AddHunMember()
            {
                CurrentTeam.HunMemberList.Add(new Member(Camp.Hun));
                RemoveHunMemberCommand.NotifyCanExecuteChanged();
                var canOthersOnField = CurrentTeam.CanAddMemberInPlayer(Camp.Hun);
                RefreshCanMemberOnFieldState(Camp.Hun);
            }

            /// <summary>
            /// 判断是否可以添加监管者成员
            /// </summary>
            /// <returns>始终返回true允许添加</returns>
            private bool CanAddHunMember() => true;

            /// <summary>
            /// 移除监管者成员命令执行方法
            /// 显示确认对话框后执行移除操作
            /// </summary>
            /// <param name="member">要移除的成员对象</param>
            /// <returns>异步操作任务</returns>
            [RelayCommand(CanExecute = nameof(CanRemoveHunMember))]
            private async Task RemoveHunMemberAsync(Member member)
            {
                await RemoveMemberAsync(member);
            }

            /// <summary>
            /// 通用成员移除异步方法
            /// 包含确认对话框和状态更新逻辑
            /// </summary>
            /// <param name="member">要移除的成员对象</param>
            /// <returns>异步操作任务</returns>
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
                    RefreshCanMemberOnFieldState(member.Camp);
                }
            }

            /// <summary>
            /// 判断是否可以移除监管者成员
            /// </summary>
            /// <returns>当成员数量大于1时返回true</returns>
            private bool CanRemoveHunMember() => CurrentTeam.HunMemberList.Count > 1;

            /// <summary>
            /// 切换成员在场状态命令执行方法
            /// 根据当前状态添加或移除场上成员
            /// </summary>
            /// <param name="member">要操作的成员对象</param>
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

            /// <summary>
            /// 刷新成员上场状态限制
            /// 根据阵营更新所有非在场成员的可操作状态
            /// </summary>
            /// <param name="camp">要刷新的阵营类型</param>
            private void RefreshCanMemberOnFieldState(Camp camp)
            {
                var canOthersOnField = CurrentTeam.CanAddMemberInPlayer(camp);
                if (camp == Camp.Sur)
                {
                    foreach (var m in CurrentTeam.SurMemberList)
                    {
                        if (!m.IsOnField)
                        {
                            m.CanOnFieldChange = canOthersOnField;
                        }
                    }
                }
                else
                {
                    foreach (var m in CurrentTeam.HunMemberList)
                    {
                        if (!m.IsOnField)
                        {
                            m.CanOnFieldChange = canOthersOnField;
                        }
                    }
                }
            }

            /// <summary>
            /// 设置成员头像命令执行方法
            /// 通过文件选择器获取图片并更新成员头像
            /// </summary>
            /// <param name="member">要操作的成员对象</param>
            [RelayCommand]
            private void SetMemberImage(Member member)
            {
                var imagePath = _filePickerService.PickImage();
                if (imagePath == null)
                    return;

                member.Image = new BitmapImage(new Uri(imagePath));
            }
        }
    }
}