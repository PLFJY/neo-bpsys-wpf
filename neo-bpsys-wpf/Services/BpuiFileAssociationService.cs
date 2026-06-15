using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Services.Abstractions;
using System.Diagnostics;
using System.IO;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// Registry-backed Windows file association manager for <c>.bpui</c> files.
/// </summary>
public sealed class BpuiFileAssociationService : IBpuiFileAssociationService
{
    private const string Extension = ".bpui";
    private const string ProgId = AppConstants.AppName + ".bpui";
    private const string Description = "BP UI Layout Package";

    private readonly ILogger<BpuiFileAssociationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BpuiFileAssociationService"/> class.
    /// </summary>
    /// <param name="logger">Logger.</param>
    public BpuiFileAssociationService(ILogger<BpuiFileAssociationService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public bool IsAssociated()
    {
        try
        {
            using var extensionKey = Registry.ClassesRoot.OpenSubKey(Extension, writable: false);
            if (!string.Equals(extensionKey?.GetValue(null) as string, ProgId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            using var commandKey = Registry.ClassesRoot.OpenSubKey($@"{ProgId}\shell\open\command", writable: false);
            var command = commandKey?.GetValue(null) as string;
            return string.Equals(command, GetOpenCommand(), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to check bpui file association.");
            return false;
        }
    }

    /// <inheritdoc/>
    public void Associate()
    {
        try
        {
            using var extensionKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{Extension}");
            extensionKey?.SetValue(null, ProgId, RegistryValueKind.String);

            using var progIdKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}");
            progIdKey?.SetValue(null, Description, RegistryValueKind.String);

            using var iconKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}\DefaultIcon");
            iconKey?.SetValue(null, $"{GetExecutablePath()},0", RegistryValueKind.String);

            using var commandKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}\shell\open\command");
            commandKey?.SetValue(null, GetOpenCommand(), RegistryValueKind.String);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to associate bpui files.");
        }
    }

    /// <inheritdoc/>
    public void RemoveAssociation()
    {
        try
        {
            using var extensionKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{Extension}", writable: true);
            if (string.Equals(extensionKey?.GetValue(null) as string, ProgId, StringComparison.OrdinalIgnoreCase))
            {
                extensionKey.DeleteValue(null, throwOnMissingValue: false);
            }

            using var classesKey = Registry.CurrentUser.OpenSubKey("Software\\Classes", writable: true);
            classesKey?.DeleteSubKeyTree(ProgId, throwOnMissingSubKey: false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove bpui file association.");
        }
    }

    /// <inheritdoc/>
    public void EnsureAssociationState(bool shouldAssociate)
    {
        if (shouldAssociate)
        {
            if (!IsAssociated())
            {
                Associate();
            }

            return;
        }

        RemoveAssociation();
    }

    private static string GetOpenCommand()
    {
        return $"\"{GetExecutablePath()}\" \"%1\"";
    }

    private static string GetExecutablePath()
    {
        return Environment.ProcessPath
               ?? Process.GetCurrentProcess().MainModule?.FileName
               ?? Path.ChangeExtension(typeof(App).Assembly.Location, ".exe");
    }
}
