using System.IO;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

namespace neo_bpsys_wpf.SmartBp.Module.Services.Recognition;

/// <summary>
/// 从 SmartBP 模块资源加载本地视觉模型使用的系统提示词 profile。
/// </summary>
internal sealed class SmartBpPromptProfileProvider : ISmartBpPromptProfileProvider
{
    private readonly ISmartBpModuleStorageProvider? _storage;

    /// <summary>
    /// 初始化从应用基目录读取提示词资源的提供程序。
    /// </summary>
    public SmartBpPromptProfileProvider()
    {
    }

    /// <summary>
    /// 初始化从 SmartBP 模块目录读取提示词资源的提供程序。
    /// </summary>
    /// <param name="storage">SmartBP 模块存储提供程序。</param>
    public SmartBpPromptProfileProvider(ISmartBpModuleStorageProvider storage)
    {
        _storage = storage;
    }

    private static readonly (string Id, string DisplayName)[] Profiles =
    [
        ("zh-CN", "简体中文 (zh-CN)"),
        ("en-US", "English (en-US)"),
        ("ja-JP", "日本語 (ja-JP)")
    ];

    /// <inheritdoc />
    public async Task<IReadOnlyList<SmartBpPromptProfile>> GetAvailableProfilesAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<SmartBpPromptProfile>();
        foreach (var profile in Profiles) results.Add(await LoadAsync(profile.Id, cancellationToken));
        return results;
    }

    /// <inheritdoc />
    public async Task<SmartBpPromptProfile> LoadAsync(string profileId, CancellationToken cancellationToken = default)
    {
        var profile = Profiles.SingleOrDefault(x => x.Id.Equals(profileId, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(profile.Id)) throw new InvalidDataException($"Unknown prompt profile '{profileId}'.");
        var path = Path.Combine(_storage?.ModuleRoot ?? AppContext.BaseDirectory, "Resources", "SmartBp", "Prompts", $"{profile.Id}.system.md");
        var prompt = await File.ReadAllTextAsync(path, cancellationToken);
        if (string.IsNullOrWhiteSpace(prompt)) throw new InvalidDataException($"Prompt profile '{profile.Id}' is empty.");
        return new(profile.Id, profile.DisplayName, prompt.Trim());
    }
}
