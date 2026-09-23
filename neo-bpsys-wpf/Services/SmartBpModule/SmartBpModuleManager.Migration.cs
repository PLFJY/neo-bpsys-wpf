using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.Archives;
using neo_bpsys_wpf.Core.Models.SmartBpModule;
using neo_bpsys_wpf.ProductTour;

namespace neo_bpsys_wpf.Services.SmartBpModule;

/// <summary>
/// SmartBP 模块管理器的Migration逻辑。
/// </summary>
public sealed partial class SmartBpModuleManager
{
    /// <summary>
    /// 写入 SmartBP 模块状态文件，并把模块根目录镜像到 HKCU 供卸载器清理使用。
    /// </summary>
    /// <param name="state">要持久化的状态。</param>
    private void WriteState(SmartBpModuleState state)
    {
        Directory.CreateDirectory(AppConstants.AppDataPath);
        File.WriteAllText(StateFilePath, JsonSerializer.Serialize(state, JsonOptions));
        WriteModuleRootRegistryValue(state.ModuleRoot);
        _logger.LogDebug(
            "SmartBP module state written. StateFilePath={StateFilePath}, ModuleRoot={ModuleRoot}, InstallKind={InstallKind}, LastLoadedSuccessfully={LastLoadedSuccessfully}",
            StateFilePath,
            state.ModuleRoot,
            state.InstallKind,
            state.LastLoadedSuccessfully);
    }

    /// <summary>
    /// 将模块根目录保存到当前用户注册表配置单元，供不便解析 JSON 状态文件的工具使用。
    /// </summary>
    /// <param name="moduleRoot">要写入的模块根目录路径。</param>
    private void WriteModuleRootRegistryValue(string? moduleRoot)
    {
        if (string.IsNullOrWhiteSpace(moduleRoot))
        {
            _logger.LogDebug("Skipped SmartBP module registry update because ModuleRoot is empty.");
            return;
        }

        try
        {
            var normalizedRoot = Path.GetFullPath(moduleRoot);
            using var key = Registry.CurrentUser.CreateSubKey(ModuleRegistrySubKey);
            key?.SetValue(ModuleRegistryRootValueName, normalizedRoot, RegistryValueKind.String);
            _logger.LogDebug(
                "SmartBP module registry path written. SubKey={SubKey}, ValueName={ValueName}, ModuleRoot={ModuleRoot}",
                ModuleRegistrySubKey,
                ModuleRegistryRootValueName,
                normalizedRoot);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write SmartBP module registry path for uninstall cleanup. ModuleRoot={ModuleRoot}", moduleRoot);
        }
    }

    /// <summary>
    /// 暂存导入的模块压缩包，使下一次进程启动时可以替换当前活动模块目录。
    /// </summary>
    /// <param name="candidateRoot">临时解压的候选模块根目录。</param>
    /// <param name="targetRoot">重启时应被替换的最终模块根目录。</param>
    /// <param name="installKind">写入模块状态的安装来源标签。</param>
    /// <param name="manifest">已校验的模块清单（若读取成功）。</param>
    /// <exception cref="InvalidOperationException">暂存路径与目标路径重叠时抛出。</exception>
    private void PrepareArchiveImportForRestart(
        string candidateRoot,
        string targetRoot,
        string installKind,
        SmartBpModuleManifest? manifest)
    {
        var normalizedCandidateRoot = Path.GetFullPath(candidateRoot);
        var normalizedTargetRoot = Path.GetFullPath(targetRoot);
        var pendingParent = Path.Combine(AppConstants.AppDataPath, PendingArchiveImportDirectoryName);
        var preparedRoot = Path.Combine(pendingParent, Guid.NewGuid().ToString("N"));
        var state = ReadState();

        if (IsSameOrChildPath(preparedRoot, normalizedTargetRoot) ||
            IsSameOrChildPath(normalizedTargetRoot, preparedRoot))
        {
            throw new InvalidOperationException("Pending SmartBP module path overlaps the target module path.");
        }

        // 已加载模块可能持有 native DLL 句柄，因此压缩包导入先暂存，
        // 并在下一次进程启动、模块加载前完成替换。
        CleanupExistingPendingArchiveImport();
        Directory.CreateDirectory(pendingParent);
        Directory.Move(normalizedCandidateRoot, preparedRoot);

        WriteMovePendingState(new SmartBpModuleMovePendingState
        {
            SourceRoot = normalizedTargetRoot,
            TargetRoot = normalizedTargetRoot,
            PreparedRoot = preparedRoot,
            InstallKind = installKind,
            CreatedAt = DateTimeOffset.UtcNow
        });

        WriteState(new SmartBpModuleState
        {
            ModuleRoot = normalizedTargetRoot,
            ModuleVersion = manifest?.ModuleVersion ?? state?.ModuleVersion,
            RuntimeAbiVersion = manifest?.RuntimeAbiVersion ?? state?.RuntimeAbiVersion,
            Rid = manifest?.Rid ?? state?.Rid,
            InstallKind = installKind,
            LastLoadedSuccessfully = false,
            LastLoadedAt = null,
            LegacyOcrModelMigration = state?.LegacyOcrModelMigration ?? new SmartBpLegacyOcrModelMigrationState()
        });

        ModuleRoot = normalizedTargetRoot;
        LastFailureMessage = string.Empty;
        IsRestartRequiredForPendingModuleImport = true;
        _logger.LogInformation(
            "SmartBP module archive import staged for next restart. PreparedRoot={PreparedRoot}, TargetRoot={TargetRoot}, InstallKind={InstallKind}",
            preparedRoot,
            normalizedTargetRoot,
            installKind);
        ModuleStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 当新的压缩包导入取代旧导入时，移除先前暂存的导入目录。
    /// </summary>
    private void CleanupExistingPendingArchiveImport()
    {
        var pending = ReadMovePendingState();
        if (string.IsNullOrWhiteSpace(pending?.PreparedRoot))
        {
            return;
        }

        try
        {
            if (Directory.Exists(pending.PreparedRoot))
            {
                Directory.Delete(pending.PreparedRoot, recursive: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean previous pending SmartBP module archive import: {PreparedRoot}", pending.PreparedRoot);
        }
    }

    /// <summary>
    /// 在当前进程尝试加载 SmartBP 前完成已暂存的压缩包导入。
    /// </summary>
    /// <param name="pending">从磁盘读取的待完成移动/导入标记。</param>
    /// <returns>没有剩余操作或替换成功时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
    private bool TryCompletePendingArchiveImport(SmartBpModuleMovePendingState pending)
    {
        if (string.IsNullOrWhiteSpace(pending.PreparedRoot) ||
            string.IsNullOrWhiteSpace(pending.TargetRoot))
        {
            return true;
        }

        try
        {
            var preparedRoot = Path.GetFullPath(pending.PreparedRoot);
            var targetRoot = Path.GetFullPath(pending.TargetRoot);
            if (!Directory.Exists(preparedRoot))
            {
                pending.LastCleanupError = "Prepared SmartBP module directory is missing.";
                WriteMovePendingState(pending);
                _logger.LogWarning(
                    "Pending SmartBP module archive import cannot continue because prepared directory is missing: {PreparedRoot}",
                    preparedRoot);
                return false;
            }

            if (IsUnsafeInstallPath(targetRoot) ||
                IsSameOrChildPath(preparedRoot, targetRoot) ||
                IsSameOrChildPath(targetRoot, preparedRoot))
            {
                pending.LastCleanupError = "Pending SmartBP module replacement path is unsafe.";
                WriteMovePendingState(pending);
                _logger.LogWarning(
                    "Pending SmartBP module archive import rejected unsafe paths. PreparedRoot={PreparedRoot}, TargetRoot={TargetRoot}",
                    preparedRoot,
                    targetRoot);
                return false;
            }

            if (!ValidateModuleDirectory(
                    preparedRoot,
                    allowDevelopmentDirectory: false,
                    out _,
                    out var validationError))
            {
                pending.LastCleanupError = validationError;
                WriteMovePendingState(pending);
                _logger.LogWarning(
                    "Pending SmartBP module archive import failed validation. PreparedRoot={PreparedRoot}, Error={Error}",
                    preparedRoot,
                    validationError);
                return false;
            }

            // 运行时托管模型可能体积较大且由用户下载；替换代码/资源时
            // 保留同一模块根目录下的 OCR 或 AI 资产。
            ReplaceModuleRootPreservingManagedAssets(preparedRoot, targetRoot);
            File.Delete(MovePendingFilePath);
            _logger.LogInformation("Completed pending SmartBP module archive import: {TargetRoot}", targetRoot);
            return true;
        }
        catch (Exception ex)
        {
            pending.LastCleanupError = FormatExceptionForUser(ex);
            WriteMovePendingState(pending);
            _logger.LogWarning(ex, "Failed to complete pending SmartBP module archive import.");
            return false;
        }
    }

    /// <summary>
    /// 通过已校验的暂存目录，把现有模块根目录复制到新的目标根目录。
    /// </summary>
    /// <param name="sourceRoot">现有模块根目录。</param>
    /// <param name="targetRoot">目标模块根目录。</param>
    /// <returns>复制与替换完成后结束的任务。</returns>
    /// <exception cref="InvalidOperationException">目标目录或暂存副本不是有效的 SmartBP 模块时抛出。</exception>
    private async Task CopyModuleRootForMigrationAsync(string sourceRoot, string targetRoot)
    {
        var targetExists = Directory.Exists(targetRoot);
        if (targetExists && Directory.EnumerateFileSystemEntries(targetRoot).Any())
        {
            if (!ValidateModuleDirectory(
                    targetRoot,
                    allowDevelopmentDirectory: IsDebugBuild(),
                    out _,
                    out var targetValidationError))
            {
                throw new InvalidOperationException(
                    $"Target directory already contains non-SmartBP files or an invalid module: {targetValidationError}");
            }
        }

        var tempRoot = Path.Combine(AppConstants.AppTempPath, "SmartBpModuleMove", Guid.NewGuid().ToString("N"));
        var stagingRoot = Path.Combine(tempRoot, "SmartBpModule");
        Directory.CreateDirectory(tempRoot);
        try
        {
            await Task.Run(() => CopyDirectory(sourceRoot, stagingRoot));
            if (!ValidateModuleDirectory(
                    stagingRoot,
                    allowDevelopmentDirectory: IsDebugBuild(),
                    out _,
                    out var stagingValidationError))
            {
                throw new InvalidOperationException($"Copied module failed validation: {stagingValidationError}");
            }

            if (Directory.Exists(targetRoot))
            {
                _logger.LogInformation("Replacing SmartBP module migration target: {TargetRoot}", targetRoot);
                Directory.Delete(targetRoot, recursive: true);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetRoot)!);
            Directory.Move(stagingRoot, targetRoot);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 读取待完成的模块移动或压缩包导入标记。
    /// </summary>
    /// <returns>待完成移动状态；标记不存在或无法读取时返回 <see langword="null"/>。</returns>
    private SmartBpModuleMovePendingState? ReadMovePendingState()
    {
        try
        {
            if (!File.Exists(MovePendingFilePath))
            {
                return null;
            }

            return JsonSerializer.Deserialize<SmartBpModuleMovePendingState>(
                File.ReadAllText(MovePendingFilePath),
                JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read SmartBP module move marker: {Marker}", MovePendingFilePath);
            return null;
        }
    }

    /// <summary>
    /// 写入待完成的模块移动或压缩包导入标记。
    /// </summary>
    /// <param name="state">移动标记状态。</param>
    private void WriteMovePendingState(SmartBpModuleMovePendingState state)
    {
        Directory.CreateDirectory(AppConstants.AppDataPath);
        File.WriteAllText(MovePendingFilePath, JsonSerializer.Serialize(state, JsonOptions));
        _logger.LogDebug(
            "SmartBP module move marker written. SourceRoot={SourceRoot}, TargetRoot={TargetRoot}, Marker={Marker}",
            state.SourceRoot,
            state.TargetRoot,
            MovePendingFilePath);
    }

    /// <summary>
    /// 切换到开发目录且不应继续此前移动时，删除待完成移动标记。
    /// </summary>
    private void DeleteMovePendingStateIfExists()
    {
        try
        {
            if (!File.Exists(MovePendingFilePath))
            {
                return;
            }

            File.Delete(MovePendingFilePath);
            _logger.LogInformation("Deleted SmartBP module move marker while switching to development directory: {Marker}", MovePendingFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete SmartBP module move marker while switching to development directory: {Marker}", MovePendingFilePath);
        }
    }

    /// <summary>
    /// 迁移后的目标根目录成功加载后清理旧模块根目录。
    /// </summary>
    /// <param name="loadedModuleRoot">刚刚完成加载的模块根目录。</param>
    private void CompletePendingModuleRootMigration(string loadedModuleRoot)
    {
        var pending = ReadMovePendingState();
        if (pending == null)
        {
            return;
        }

        var normalizedLoadedRoot = Path.GetFullPath(loadedModuleRoot);
        var normalizedTargetRoot = Path.GetFullPath(pending.TargetRoot);
        var normalizedSourceRoot = Path.GetFullPath(pending.SourceRoot);
        if (!string.Equals(normalizedLoadedRoot, normalizedTargetRoot, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug(
                "Skipping SmartBP module move cleanup because loaded root is not pending target. LoadedRoot={LoadedRoot}, TargetRoot={TargetRoot}",
                normalizedLoadedRoot,
                normalizedTargetRoot);
            return;
        }

        if (!string.IsNullOrWhiteSpace(pending.PreparedRoot))
        {
            _logger.LogDebug(
                "Skipping SmartBP module move cleanup because an archive import is still pending. PreparedRoot={PreparedRoot}, TargetRoot={TargetRoot}",
                pending.PreparedRoot,
                normalizedTargetRoot);
            return;
        }

        if (string.Equals(normalizedSourceRoot, normalizedTargetRoot, StringComparison.OrdinalIgnoreCase) ||
            IsSameOrChildPath(normalizedTargetRoot, normalizedSourceRoot) ||
            IsSameOrChildPath(normalizedSourceRoot, normalizedTargetRoot))
        {
            _logger.LogWarning(
                "SmartBP module move marker contains unsafe source/target pair. SourceRoot={SourceRoot}, TargetRoot={TargetRoot}",
                normalizedSourceRoot,
                normalizedTargetRoot);
            File.Delete(MovePendingFilePath);
            return;
        }

        try
        {
            if (Directory.Exists(normalizedSourceRoot))
            {
                Directory.Delete(normalizedSourceRoot, recursive: true);
                _logger.LogInformation(
                    "Deleted old SmartBP module directory after successful target load. SourceRoot={SourceRoot}, TargetRoot={TargetRoot}",
                    normalizedSourceRoot,
                    normalizedTargetRoot);
            }

            File.Delete(MovePendingFilePath);
        }
        catch (Exception ex)
        {
            pending.LastCleanupError = FormatExceptionForUser(ex);
            WriteMovePendingState(pending);
            _logger.LogWarning(
                ex,
                "SmartBP module target loaded, but old directory cleanup is still pending. SourceRoot={SourceRoot}, TargetRoot={TargetRoot}",
                normalizedSourceRoot,
                normalizedTargetRoot);
        }
    }

    /// <summary>
    /// 将旧 Documents 位置中的旧版 OCR 模型目录一次性迁移到 SmartBP 模块根目录。
    /// </summary>
    /// <param name="moduleRoot">当前 SmartBP 模块根目录。</param>
    /// <returns>迁移状态记录完成后结束的任务。</returns>
    private async Task MigrateLegacyOcrModelsOnceAsync(string moduleRoot)
    {
        var state = ReadState();
        if (state?.LegacyOcrModelMigration.Completed == true)
        {
            _logger.LogDebug("Skipping legacy OCR model migration because it has already completed.");
            return;
        }

        var oldRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "neo-bpsys-wpf", "OCRModels");
        var migration = new SmartBpLegacyOcrModelMigrationState();
        if (!Directory.Exists(oldRoot))
        {
            _logger.LogInformation("No legacy OCR model root found. OldRoot={OldRoot}", oldRoot);
            migration.Completed = true;
            migration.Reason = "NoLegacyModels";
            WriteState(new SmartBpModuleState { ModuleRoot = moduleRoot, LegacyOcrModelMigration = migration });
            return;
        }

        var newRoot = Path.Combine(moduleRoot, "OCRModels");
        Directory.CreateDirectory(newRoot);
        foreach (var key in KnownOcrModelKeys)
        {
            var modelDir = Path.Combine(oldRoot, key);
            if (!Directory.Exists(modelDir))
                continue;

            if (!IsLegacyModelReady(modelDir))
            {
                _logger.LogDebug("Skipping incomplete legacy OCR model. ModelKey={ModelKey}, ModelDir={ModelDir}", key, modelDir);
                continue;
            }
            var staging = Path.Combine(newRoot, $"{key}.staging");
            var target = Path.Combine(newRoot, key);
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
            CopyDirectory(modelDir, staging);
            if (!IsLegacyModelReady(staging))
            {
                _logger.LogWarning("Legacy OCR model copy verification failed. ModelKey={ModelKey}, Staging={Staging}", key, staging);
                Directory.Delete(staging, recursive: true);
                continue;
            }
            if (Directory.Exists(target))
                Directory.Delete(target, recursive: true);
            Directory.Move(staging, target);
            try
            {
                Directory.Delete(modelDir, recursive: true);
                _logger.LogInformation("Migrated legacy OCR model. ModelKey={ModelKey}, Target={Target}", key, target);
            }
            catch
            {
                _logger.LogWarning("Legacy OCR model migrated but cleanup is pending. ModelKey={ModelKey}, ModelDir={ModelDir}", key, modelDir);
                migration.PendingCleanupModelKeys.Add(key);
            }
        }

        migration.Completed = true;
        migration.Reason = migration.PendingCleanupModelKeys.Count == 0 ? "Completed" : "PendingCleanup";
        WriteState(new SmartBpModuleState { ModuleRoot = moduleRoot, LegacyOcrModelMigration = migration });
        await Task.CompletedTask;
    }

    /// <summary>
    /// 判断旧版 PaddleOCR 模型目录是否包含所有必需的推理组件。
    /// </summary>
    /// <param name="modelRoot">旧版模型根目录。</param>
    /// <returns>当 det、cls 和 rec 组件都存在时返回 <see langword="true"/>。</returns>
    private static bool IsLegacyModelReady(string modelRoot) =>
        new[] { "det", "cls", "rec" }.All(component =>
        {
            var dir = Path.Combine(modelRoot, component);
            return File.Exists(Path.Combine(dir, "inference.pdiparams")) &&
                   (File.Exists(Path.Combine(dir, "inference.pdmodel")) ||
                    File.Exists(Path.Combine(dir, "inference.json")));
        });

    /// <summary>
    /// 递归复制目录树并保留相对路径。
    /// </summary>
    /// <param name="source">源目录。</param>
    /// <param name="target">目标目录。</param>
    private static void CopyDirectory(string source, string target)
    {
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, directory)));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }

    /// <summary>
    /// 替换模块文件，同时保留运行时托管的 OCR 和 AI 资产目录。
    /// </summary>
    /// <param name="sourceRoot">已校验的替换模块根目录。</param>
    /// <param name="targetRoot">要更新的现有模块根目录。</param>
    private void ReplaceModuleRootPreservingManagedAssets(string sourceRoot, string targetRoot)
    {
        var normalizedSourceRoot = Path.GetFullPath(sourceRoot);
        var normalizedTargetRoot = Path.GetFullPath(targetRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(normalizedTargetRoot)!);

        if (!Directory.Exists(normalizedTargetRoot))
        {
            Directory.Move(normalizedSourceRoot, normalizedTargetRoot);
            return;
        }

        _logger.LogInformation(
            "Replacing SmartBP module target root while preserving managed model assets. TargetRoot={TargetRoot}",
            normalizedTargetRoot);

        foreach (var entry in Directory.EnumerateFileSystemEntries(normalizedTargetRoot).ToArray())
        {
            if (IsManagedAssetRoot(entry))
            {
                _logger.LogDebug("Preserving SmartBP managed asset directory during module replacement: {Path}", entry);
                continue;
            }

            DeleteFileSystemEntry(entry);
        }

        foreach (var entry in Directory.EnumerateFileSystemEntries(normalizedSourceRoot).ToArray())
        {
            var name = Path.GetFileName(entry);
            var destination = Path.Combine(normalizedTargetRoot, name);
            if (ManagedAssetRootNames.Contains(name) && Directory.Exists(destination))
            {
                _logger.LogInformation(
                    "Skipping packaged SmartBP managed asset directory because an existing downloaded asset directory is present. Path={Path}",
                    destination);
                continue;
            }

            if (Directory.Exists(destination) || File.Exists(destination))
            {
                DeleteFileSystemEntry(destination);
            }

            if (Directory.Exists(entry))
            {
                Directory.Move(entry, destination);
            }
            else
            {
                File.Move(entry, destination, overwrite: true);
            }
        }
    }

    /// <summary>
    /// 判断路径是否属于 SmartBP 运行时托管资产根目录之一。
    /// </summary>
    /// <param name="path">要检查的路径。</param>
    /// <returns>当路径是已知的托管资产目录时返回 <see langword="true"/>。</returns>
    private static bool IsManagedAssetRoot(string path)
    {
        return Directory.Exists(path) && ManagedAssetRootNames.Contains(Path.GetFileName(path));
    }

    /// <summary>
    /// 当文件系统项是已存在的文件或目录时删除它。
    /// </summary>
    /// <param name="path">要删除的文件或目录路径。</param>
    private static void DeleteFileSystemEntry(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
            return;
        }

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// 在完整路径标准化后，判断一个路径是否等于或嵌套在另一个路径下。
    /// </summary>
    /// <param name="child">候选子路径。</param>
    /// <param name="parent">候选父路径。</param>
    /// <returns>当 <paramref name="child"/> 等于或位于 <paramref name="parent"/> 之下时返回 <see langword="true"/>。</returns>
    private static bool IsSameOrChildPath(string child, string parent)
    {
        var normalizedChild = Path.GetFullPath(child)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedParent = Path.GetFullPath(parent)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return normalizedChild.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 通过在最近的已存在目录中创建临时探测文件检查写入权限。
    /// </summary>
    /// <param name="path">请求的模块路径或父路径。</param>
    /// <returns>当可以创建临时文件时返回 <see langword="true"/>。</returns>
    private static bool HasWriteAccess(string path)
    {
        try
        {
            var probeDirectory = Directory.Exists(path)
                ? path
                : GetNearestExistingDirectory(path);
            if (string.IsNullOrWhiteSpace(probeDirectory))
                return false;

            var probePath = Path.Combine(probeDirectory, $".smartbp-write-test-{Guid.NewGuid():N}.tmp");
            using (File.Create(probePath, 1, FileOptions.DeleteOnClose))
            {
            }

            if (File.Exists(probePath))
                File.Delete(probePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 从给定路径向上查找，直到找到已存在的目录。
    /// </summary>
    /// <param name="path">可能尚不存在的路径。</param>
    /// <returns>最近的已存在目录；找不到时返回 <see langword="null"/>。</returns>
    private static string? GetNearestExistingDirectory(string path)
    {
        var current = Path.GetDirectoryName(Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (Directory.Exists(current))
                return current;

            current = Path.GetDirectoryName(current);
        }

        return null;
    }

    /// <summary>
    /// 报告当前程序集是否使用 DEBUG 符号编译。
    /// </summary>
    /// <returns>调试构建时返回 <see langword="true"/>。</returns>
    private static bool IsDebugBuild()
    {
#if DEBUG
        return true;
#else
        return false;
#endif
    }

    /// <summary>
    /// 报告当前程序集是否使用 PREVIEW 符号编译。
    /// </summary>
    /// <returns>预览构建时返回 <see langword="true"/>。</returns>
    private static bool IsPreviewBuild()
    {
#if PREVIEW
        return true;
#else
        return false;
#endif
    }

    /// <summary>
    /// 获取与当前应用版本匹配的发布标签中附带的 SmartBP 模块 manifest。
    /// 优先直接访问 GitHub Releases 下载当前 tag 对应的 <c>SmartBpModuleManifest.json</c>，
    /// 失败时回退到 <c>https://smartbp-module-manifest.plfjy.top/?tag={tag}</c> 获取。
    /// 只检查当前应用版本对应的 tag，不会获取最新 release。
    /// </summary>
    /// <returns>要求的模块 manifest；无法获取发布元数据时返回 <see langword="null"/>。</returns>
    private async Task<SmartBpModuleManifest?> TryFetchCurrentTagManifestAsync()
    {
        var tag = AppConstants.AppVersion;
        if (string.IsNullOrWhiteSpace(tag))
        {
            _logger.LogWarning("Skipped SmartBP module manifest fetch because current app version is empty.");
            return null;
        }

        var encodedTag = Uri.EscapeDataString(tag);

        // 优先直接访问 GitHub Releases 获取当前 tag 的 SmartBpModuleManifest.json
        var githubUrl = $"{GitHubReleaseDownloadBaseUrl}/{encodedTag}/{ModuleManifestAssetName}";
        var manifest = await TryDownloadModuleManifestAsync(githubUrl, "GitHub Releases");
        if (manifest != null)
            return manifest;

        // Fallback 到 smartbp-module-manifest.plfjy.top
        var fallbackUrl = $"{ModuleManifestFallbackBaseUrl}?tag={encodedTag}";
        return await TryDownloadModuleManifestAsync(fallbackUrl, "fallback manifest proxy");
    }

    /// <summary>
    /// 从指定 URL 下载并反序列化 SmartBP 模块 manifest。
    /// </summary>
    /// <param name="url">manifest JSON 下载地址。</param>
    /// <param name="sourceLabel">用于日志的来源标签。</param>
    /// <returns>成功下载并反序列化时返回 manifest；失败时返回 <see langword="null"/>。</returns>
    private async Task<SmartBpModuleManifest?> TryDownloadModuleManifestAsync(string url, string sourceLabel)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(AppConstants.AppName);
            _logger.LogInformation("Downloading SmartBP module manifest from {Source}. Url={Url}", sourceLabel, url);
            var json = await httpClient.GetStringAsync(url);
            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning("SmartBP module manifest content was empty from {Source}. Url={Url}", sourceLabel, url);
                return null;
            }

            return JsonSerializer.Deserialize<SmartBpModuleManifest>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch SmartBP module manifest from {Source}. Url={Url}", sourceLabel, url);
            return null;
        }
    }

    /// <summary>
    /// 为中文用户应用已配置的 GitHub 代理镜像。
    /// </summary>
    /// <param name="url">原始下载 URL。</param>
    /// <returns>已配置镜像时返回镜像 URL；否则返回原始 URL。</returns>
    private string GetMirroredDownloadUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return url;

        if (!_settingsHostService.Settings.CultureInfo.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            return url;

        var mirror = _settingsHostService.Settings.GhProxyMirror;
        return string.IsNullOrWhiteSpace(mirror) ? url : mirror + url;
    }

    /// <summary>
    /// 将异常链扁平化为紧凑的用户可读诊断字符串。
    /// </summary>
    /// <param name="exception">要格式化的异常。</param>
    /// <returns>去重后的消息摘要。</returns>
    private static string FormatExceptionForUser(Exception exception)
    {
        var messages = new List<string>();
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (!string.IsNullOrWhiteSpace(current.Message))
                messages.Add(current.Message);
        }

        if (exception is ReflectionTypeLoadException reflectionTypeLoadException)
        {
            messages.AddRange(reflectionTypeLoadException.LoaderExceptions
                .Where(e => !string.IsNullOrWhiteSpace(e?.Message))
                .Select(e => e!.Message));
        }

        return string.Join(" | ", messages.Distinct(StringComparer.Ordinal));
    }

}
