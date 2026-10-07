using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Models.RemoteAnnouncements;

/// <summary>远程公告清单。</summary>
public sealed class RemoteAnnouncementManifest
{
    /// <summary>清单格式版本。</summary>
    public int SchemaVersion { get; set; }

    /// <summary>清单公告。</summary>
    public List<RemoteAnnouncementManifestEntry> Announcements { get; set; } = [];
}

/// <summary>清单中的公告元数据。</summary>
public sealed class RemoteAnnouncementManifestEntry
{
    /// <summary>公告 ID。</summary>
    public string Id { get; set; } = "";

    /// <summary>相对于 GitCode Raw 基础地址的公告正文路径；缺失时使用公告 ID 对应的标准路径。</summary>
    public string? Path { get; set; }

    /// <summary>发布时间。</summary>
    public DateTimeOffset PublishedAt { get; set; }

    /// <summary>更新时间。</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>是否启用。</summary>
    public bool Enabled { get; set; }

    /// <summary>正文修订号。</summary>
    public int Revision { get; set; }

    /// <summary>正文原始字节的 SHA-256。</summary>
    public string Sha256 { get; set; } = "";

    /// <summary>最低应用版本，包含边界。</summary>
    public string? MinAppVersion { get; set; }

    /// <summary>最高应用版本，包含边界。</summary>
    public string? MaxAppVersion { get; set; }

    /// <summary>适用发布通道。</summary>
    public List<string> Channels { get; set; } = [];
}

/// <summary>公告级别。</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RemoteAnnouncementLevel>))]
public enum RemoteAnnouncementLevel
{
    /// <summary>普通信息。</summary>
    Info,
    /// <summary>警告。</summary>
    Warning,
    /// <summary>重要公告。</summary>
    Critical
}

/// <summary>远程公告正文。</summary>
public sealed class RemoteAnnouncement
{
    /// <summary>正文格式版本。</summary>
    public int SchemaVersion { get; set; }

    /// <summary>公告 ID。</summary>
    public string Id { get; set; } = "";

    /// <summary>发布时间。</summary>
    public DateTimeOffset PublishedAt { get; set; }

    /// <summary>更新时间。</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>公告级别。</summary>
    public RemoteAnnouncementLevel Level { get; set; }

    /// <summary>多语言标题。</summary>
    public Dictionary<string, string> Title { get; set; } = [];

    /// <summary>多语言 Markdown 正文。</summary>
    public Dictionary<string, string> Content { get; set; } = [];

    /// <summary>最低应用版本。</summary>
    public string? MinAppVersion { get; set; }

    /// <summary>最高应用版本。</summary>
    public string? MaxAppVersion { get; set; }

    /// <summary>适用发布通道。</summary>
    public List<string> Channels { get; set; } = [];
}

/// <summary>本机公告已读状态。</summary>
public sealed class RemoteAnnouncementState
{
    /// <summary>已读公告 ID。</summary>
    public HashSet<string> SeenAnnouncementIds { get; set; } = new(StringComparer.Ordinal);
}
