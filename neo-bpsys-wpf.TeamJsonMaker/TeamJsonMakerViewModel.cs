using neo_bpsys_wpf.Core.Abstractions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.TeamJsonMaker;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.TeamJsonMaker;

public partial class TeamJsonMakerViewModel : ViewModelBase
{
    public Team CurrentTeam { get; } = new();

    [RelayCommand]
    private void AddSurMember()
    {
        CurrentTeam.SurMemberList.Add(new Member(Camp.Sur));
        RemoveSurMemberCommand.NotifyCanExecuteChanged();
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
            Title = "删除确认",
            Content = $"是否删除 {memberName}?",
            PrimaryButtonText = "是",
            PrimaryButtonIcon = new SymbolIcon { Symbol = SymbolRegular.Delete24 },
            CloseButtonIcon = new SymbolIcon { Symbol = SymbolRegular.Prohibited20 },
            CloseButtonText = "点错了"
        };
        var result = await messageBox.ShowDialogAsync();

        if (result == MessageBoxResult.Primary)
        {
            if (member.Camp == Camp.Sur)
            {
                CurrentTeam.SurMemberList.Remove(member);
            }
            else
            {
                CurrentTeam.HunMemberList.Remove(member);
            }
        }
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        var json = JsonSerializer.Serialize<Team>(CurrentTeam,
            new JsonSerializerOptions() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } });

        //打开通用对话框选择保存路径
        var dialog = new SaveFileDialog
        {
            Filter = $"JSON 文件 (*.json)|*.json|所有文件(*.*)|*.*",
            DefaultExt = ".json",
            AddExtension = true,
            DefaultDirectory = AppConstants.AppOutputPath,
            Title = "保存为",
            FileName = CurrentTeam.TeamName,
            OverwritePrompt = false
        };

        var result = (bool)dialog.ShowDialog()!;
        //如果用户没选择直接退出
        if (!result) return;

        var savePath = dialog.FileName;

        try
        {
            if (File.Exists(savePath))
            {
                if (await MessageBoxHelper.ShowConfirmAsync($"{savePath} 已存在，是否覆盖",
                        "覆盖提示", "确认", "取消"))
                    File.Delete(savePath);
                else
                {
                    return;
                }
            }

            await File.WriteAllTextAsync(savePath, json);
            //提示用户已完成
            await MessageBoxHelper.ShowInfoAsync($"队伍信息已被保存至 {savePath}，可直接在应用内导入");
        }
        catch (Exception e)
        {
            await MessageBoxHelper.ShowErrorAsync(e.Message, "队伍信息导出错误");
        }
    }
}