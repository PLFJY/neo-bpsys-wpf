using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.ProductTour;

/// <summary>
/// Persists tutorial state.
/// </summary>
public interface ITutorialStateStore
{
    /// <summary>Loads tutorial state.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded tutorial state.</returns>
    Task<TutorialState> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Saves tutorial state.</summary>
    /// <param name="state">State to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(TutorialState state, CancellationToken cancellationToken = default);

    /// <summary>Clears all tutorial state.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ResetAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// JSON file based tutorial state store.
/// </summary>
public sealed class TutorialStateStore : ITutorialStateStore
{
    private readonly string _statePath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="TutorialStateStore"/> class.
    /// </summary>
    public TutorialStateStore()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "neo-bpsys-wpf");
        _statePath = Path.Combine(appData, "TutorialState.json");
    }

    /// <inheritdoc />
    public async Task<TutorialState> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_statePath))
        {
            return new TutorialState();
        }

        try
        {
            await using var stream = File.OpenRead(_statePath);
            return await JsonSerializer.DeserializeAsync<TutorialState>(stream, _jsonOptions, cancellationToken)
                .ConfigureAwait(false) ?? new TutorialState();
        }
        catch
        {
            // 文件损坏或格式不兼容时删除并返回空状态，避免阻塞教程流程。
            TryDeleteStateFile();
            return new TutorialState();
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(TutorialState state, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_statePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_statePath);
        await JsonSerializer.SerializeAsync(stream, state, _jsonOptions, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task ResetAsync(CancellationToken cancellationToken = default)
    {
        TryDeleteStateFile();
        return Task.CompletedTask;
    }

    private void TryDeleteStateFile()
    {
        try
        {
            if (File.Exists(_statePath))
            {
                File.Delete(_statePath);
            }
        }
        catch
        {
            // 删除失败不影响后续流程，下次保存会覆盖。
        }
    }
}
