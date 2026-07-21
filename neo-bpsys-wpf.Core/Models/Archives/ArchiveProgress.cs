namespace neo_bpsys_wpf.Core.Models.Archives;

/// <summary>
/// 原生 7-Zip 解压进度。
/// </summary>
/// <param name="Percentage">0-100 进度百分比。</param>
/// <param name="CurrentFile">当前正在处理的文件名(若可用)。</param>
public readonly record struct ArchiveProgress(int Percentage, string? CurrentFile = null);
