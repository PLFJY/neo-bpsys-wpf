using System.IO;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.Archives;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace neo_bpsys_wpf.Core.Services.Archives;

/// <summary>
/// 基于 SharpCompress 的运行时归档服务，支持 ZIP 和 7z 包。
/// </summary>
public sealed class SharpCompressArchiveService : IArchiveService
{
    /// <inheritdoc />
    public Task<ArchiveFormat> DetectFormatAsync(string archivePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(archivePath))
        {
            throw new FileNotFoundException("Archive file was not found.", archivePath);
        }

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var archive = ArchiveFactory.OpenArchive(archivePath);
                return MapArchiveType(archive.Type, archivePath);
            }
            catch (SharpCompressException ex)
            {
                throw new InvalidDataException($"File is not a supported archive: {archivePath}", ex);
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ArchiveFormat> ExtractToDirectoryAsync(
        string archivePath,
        string destinationDirectory,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(archivePath))
        {
            throw new FileNotFoundException("Archive file was not found.", archivePath);
        }

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(destinationDirectory);
            var normalizedDestination = NormalizeDirectoryPath(destinationDirectory);

            try
            {
                using var archive = ArchiveFactory.OpenArchive(archivePath);
                var format = MapArchiveType(archive.Type, archivePath);
                foreach (var entry in archive.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var destinationPath = GetSafeDestinationPath(entry.Key, normalizedDestination);
                    if (entry.IsDirectory)
                    {
                        Directory.CreateDirectory(destinationPath);
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                    using var input = entry.OpenEntryStream();
                    using var output = File.Create(destinationPath);
                    input.CopyTo(output);
                }

                return format;
            }
            catch (SharpCompressException ex)
            {
                throw new InvalidDataException($"File is not a supported archive: {archivePath}", ex);
            }
        }, cancellationToken);
    }

    private static ArchiveFormat MapArchiveType(ArchiveType archiveType, string archivePath)
    {
        return archiveType switch
        {
            ArchiveType.Zip => ArchiveFormat.Zip,
            ArchiveType.SevenZip => ArchiveFormat.SevenZip,
            _ => throw new InvalidDataException($"Unsupported archive format for {archivePath}: {archiveType}.")
        };
    }

    private static string GetSafeDestinationPath(string? entryKey, string normalizedDestination)
    {
        if (string.IsNullOrWhiteSpace(entryKey))
        {
            throw new IOException("Archive entry path is empty.");
        }

        var normalizedEntryKey = entryKey.Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalizedEntryKey) || HasWindowsDrivePrefix(normalizedEntryKey))
        {
            throw new IOException($"Archive entry path is absolute: {entryKey}");
        }

        var segments = normalizedEntryKey.Split(
            Path.DirectorySeparatorChar,
            StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment == ".."))
        {
            throw new IOException($"Archive entry path contains traversal: {entryKey}");
        }

        var destinationPath = Path.GetFullPath(Path.Combine(normalizedDestination, normalizedEntryKey));
        if (!IsSameOrChildPath(destinationPath, normalizedDestination))
        {
            throw new IOException($"Archive entry path escapes destination: {entryKey}");
        }

        return destinationPath;
    }

    private static bool HasWindowsDrivePrefix(string path)
    {
        return path.Length >= 2 && path[1] == ':' && char.IsLetter(path[0]);
    }

    private static string NormalizeDirectoryPath(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool IsSameOrChildPath(string child, string parent)
    {
        var normalizedChild = NormalizeDirectoryPath(child) + Path.DirectorySeparatorChar;
        var normalizedParent = NormalizeDirectoryPath(parent) + Path.DirectorySeparatorChar;
        return normalizedChild.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
    }
}
