using System;
using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models;

public class AsgEventDto
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("logoUrl")] public string? LogoUrl { get; set; }
}

public class AsgEventDtoPagedResult
{
    [JsonPropertyName("items")] public AsgEventDto[] Items { get; set; } = Array.Empty<AsgEventDto>();
    [JsonPropertyName("totalCount")] public int TotalCount { get; set; }
    [JsonPropertyName("page")] public int Page { get; set; }
    [JsonPropertyName("pageSize")] public int PageSize { get; set; }
}

public class AsgMatchDto
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("eventId")] public string? EventId { get; set; }
    [JsonPropertyName("homeTeamId")] public string HomeTeamId { get; set; } = string.Empty;
    [JsonPropertyName("homeTeamName")] public string HomeTeamName { get; set; } = string.Empty;
    [JsonPropertyName("awayTeamId")] public string AwayTeamId { get; set; } = string.Empty;
    [JsonPropertyName("awayTeamName")] public string AwayTeamName { get; set; } = string.Empty;
}

public class AsgTeamDto
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("logoUrl")] public string? LogoUrl { get; set; }
    [JsonPropertyName("players")] public AsgPlayerDto[] Players { get; set; } = Array.Empty<AsgPlayerDto>();
}

public class AsgPlayerDto
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
}

public class AsgMatchScoresUpdateDto
{
    [JsonPropertyName("bestOf")] public int BestOf { get; set; }
    [JsonPropertyName("games")] public AsgGameScoreDto[] Games { get; set; } = Array.Empty<AsgGameScoreDto>();
}

public class AsgGameScoreDto
{
    [JsonPropertyName("home")] public int Home { get; set; }
    [JsonPropertyName("away")] public int Away { get; set; }
}
