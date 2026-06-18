using System.IO;
using neo_bpsys_wpf.Core.Models.Archives;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// Provides archive format probing and safe extraction for runtime package files.
/// </summary>
public interface IArchiveService
{
    /// <summary>
    /// Detects the archive format by probing archive content.
    /// </summary>
    /// <param name="archivePath">Archive file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The detected archive format.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the archive file does not exist.</exception>
    /// <exception cref="InvalidDataException">Thrown when the file is not a supported archive.</exception>
    Task<ArchiveFormat> DetectFormatAsync(string archivePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Safely extracts an archive into the destination directory.
    /// </summary>
    /// <param name="archivePath">Archive file path.</param>
    /// <param name="destinationDirectory">Destination directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The detected archive format.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the archive file does not exist.</exception>
    /// <exception cref="InvalidDataException">Thrown when the file is not a supported archive.</exception>
    /// <exception cref="IOException">Thrown when an archive entry path is unsafe or extraction fails.</exception>
    Task<ArchiveFormat> ExtractToDirectoryAsync(
        string archivePath,
        string destinationDirectory,
        CancellationToken cancellationToken = default);
}
