using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.TeamJsonMaker;

public partial class TeamJsonMakerViewModel : ViewModelBase
{
    private static readonly JsonSerializerOptions TeamJsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private Team _currentTeam = new();

    /// <summary>
    /// 获取当前正在编辑的队伍信息。
    /// </summary>
    public Team CurrentTeam
    {
        get => _currentTeam;
        private set => SetProperty(ref _currentTeam, value);
    }

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
        RemoveHunMemberCommand.NotifyCanExecuteChanged();
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
    private async Task ImportAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON 文件 (*.json)|*.json|所有文件(*.*)|*.*",
            DefaultExt = ".json",
            CheckFileExists = true,
            Title = "导入已有队伍 JSON"
        };

        if (Directory.Exists(AppConstants.AppOutputPath))
        {
            dialog.InitialDirectory = AppConstants.AppOutputPath;
        }

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(dialog.FileName);
            if (string.IsNullOrWhiteSpace(json))
            {
                await MessageBoxHelper.ShowErrorAsync("JSON 文件内容为空。", "队伍信息导入错误");
                return;
            }

            var importedTeam = JsonSerializer.Deserialize<ImportedTeamJson>(json, TeamJsonOptions);
            if (importedTeam == null)
            {
                await MessageBoxHelper.ShowErrorAsync("JSON 文件没有包含有效的队伍信息。", "队伍信息导入错误");
                return;
            }

            CurrentTeam = CreateTeam(importedTeam);
            RemoveSurMemberCommand.NotifyCanExecuteChanged();
            RemoveHunMemberCommand.NotifyCanExecuteChanged();
            await MessageBoxHelper.ShowInfoAsync($"已从 {dialog.FileName} 导入队伍信息。", "队伍信息导入完成");
        }
        catch (JsonException e)
        {
            await MessageBoxHelper.ShowErrorAsync(e.Message, "队伍信息导入错误");
        }
        catch (Exception e)
        {
            await MessageBoxHelper.ShowErrorAsync(e.Message, "队伍信息导入错误");
        }
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        var json = JsonSerializer.Serialize<Team>(CurrentTeam, TeamJsonOptions);

        //打开通用对话框选择保存路径
        var dialog = new SaveFileDialog
        {
            Filter = $"JSON 文件 (*.json)|*.json|所有文件(*.*)|*.*",
            DefaultExt = ".json",
            AddExtension = true,
            DefaultDirectory = AppConstants.AppOutputPath,
            Title = "保存为",
            FileName = CurrentTeam.Name,
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

    private static Team CreateTeam(ImportedTeamJson importedTeam)
    {
        var team = new Team
        {
            Name = importedTeam.Name ?? string.Empty,
            ImageUri = importedTeam.ImageUri ?? string.Empty,
            ColorHex = ColorHelper.NormalizeHexOrDefault(importedTeam.ColorHex, "#FF337FB9")
        };

        ReplaceMembers(team.SurMemberList, importedTeam.SurMemberList, Camp.Sur, 4);
        ReplaceMembers(team.HunMemberList, importedTeam.HunMemberList, Camp.Hun, 1);

        return team;
    }

    private static void ReplaceMembers(
        ICollection<Member> target,
        IEnumerable<Member>? source,
        Camp camp,
        int minimumCount)
    {
        target.Clear();
        foreach (var member in source ?? [])
        {
            member.Camp = camp;
            target.Add(member);
        }

        while (target.Count < minimumCount)
        {
            target.Add(new Member(camp));
        }
    }

    private sealed class ImportedTeamJson
    {
        public string? Name { get; set; }

        public string? ColorHex { get; set; }

        public string? ImageUri { get; set; }

        public List<Member>? SurMemberList { get; set; }

        public List<Member>? HunMemberList { get; set; }
    }
}
