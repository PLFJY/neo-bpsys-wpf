using System.IO;
using neo_bpsys_wpf.Core.Models.Archives;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 提供运行时包文件的归档格式探测和安全解压。
/// </summary>
public interface IArchiveService
{
    /// <summary>
    /// 通过探测归档内容检测归档格式。
    /// </summary>
    /// <param name="archivePath">归档文件路径。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>检测到的归档格式。</returns>
    /// <exception cref="FileNotFoundException">当归档文件不存在时抛出。</exception>
    /// <exception cref="InvalidDataException">当文件不是受支持的归档时抛出。</exception>
    Task<ArchiveFormat> DetectFormatAsync(string archivePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 将归档安全地解压到目标目录。
    /// </summary>
    /// <param name="archivePath">归档文件路径。</param>
    /// <param name="destinationDirectory">目标目录。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>检测到的归档格式。</returns>
    /// <exception cref="FileNotFoundException">当归档文件不存在时抛出。</exception>
    /// <exception cref="InvalidDataException">当文件不是受支持的归档时抛出。</exception>
    /// <exception cref="IOException">当归档条目路径不安全或解压失败时抛出。</exception>
    Task<ArchiveFormat> ExtractToDirectoryAsync(
        string archivePath,
        string destinationDirectory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 将归档安全地解压到目标目录，并在解压过程中报告进度。
    /// </summary>
    /// <param name="archivePath">归档文件路径。</param>
    /// <param name="destinationDirectory">目标目录。</param>
    /// <param name="progress">可选的解压进度报告器。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>检测到的归档格式。</returns>
    /// <exception cref="FileNotFoundException">当归档文件不存在时抛出。</exception>
    /// <exception cref="InvalidDataException">当文件不是受支持的归档时抛出。</exception>
    /// <exception cref="IOException">当归档条目路径不安全或解压失败时抛出。</exception>
    Task<ArchiveFormat> ExtractToDirectoryAsync(
        string archivePath,
        string destinationDirectory,
        IProgress<ArchiveProgress>? progress,
        CancellationToken cancellationToken = default);
}
