using neo_bpsys_wpf.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

public interface IASGService
{
    string BaseUrl { get; set; }
    string? AccessToken { get; }
    bool IsLoggedIn { get; }

    Task<bool> LoginAsync(string email, string password);
    Task<AsgEventDtoPagedResult?> SearchEventsAsync(string query, int page = 1, int pageSize = 12);
    Task<IReadOnlyList<AsgMatchDto>?> GetMatchesByEventAsync(Guid eventId, int page = 1, int pageSize = 50, int? groupIndex = null, string? groupLabel = null);
    Task<AsgTeamDto?> GetTeamAsync(Guid teamId);
}

