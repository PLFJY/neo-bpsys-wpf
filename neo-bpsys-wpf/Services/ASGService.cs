using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace neo_bpsys_wpf.Services;

public class ASGService : IASGService
{
    private readonly HttpClient _httpClient;
    private readonly CookieContainer _cookies = new();
    private string? _accessToken;

    public ASGService()
    {
        var handler = new HttpClientHandler
        {
            UseCookies = true,
            CookieContainer = _cookies,
            AutomaticDecompression = DecompressionMethods.All
        };
        _httpClient = new HttpClient(handler);
    }

    public string BaseUrl { get; set; } = "https://api.idvevent.cn";
    public string? AccessToken => _accessToken;
    public bool IsLoggedIn => !string.IsNullOrEmpty(_accessToken);

    private Uri BuildUri(string path, string? query = null)
    {
        var baseUrl = BaseUrl?.TrimEnd('/') ?? string.Empty;
        var full = string.IsNullOrEmpty(query) ? $"{baseUrl}{path}" : $"{baseUrl}{path}?{query}";
        return new Uri(full);
    }

    public async Task<bool> LoginAsync(string email, string password)
    {
        var body = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            { "email", email },
            { "password", password }
        });
        var req = new HttpRequestMessage(HttpMethod.Post, BuildUri("/api/Auth/login"))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        var resp = await _httpClient.SendAsync(req);
        if (!resp.IsSuccessStatusCode) return false;
        var content = await resp.Content.ReadAsStringAsync();
        string? token = null;
        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    var name = prop.Name.ToLowerInvariant();
                    if (name is "token" or "accessToken" or "jwt" or "bearer")
                    {
                        token = prop.Value.GetString();
                        break;
                    }
                }
            }
        }
        catch
        {
        }
        if (string.IsNullOrEmpty(token))
        {
            var authHeader = resp.Headers.Contains("Authorization")
                ? string.Join(" ", resp.Headers.GetValues("Authorization"))
                : null;
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
            {
                token = authHeader[7..];
            }
        }
        _accessToken = token;
        _httpClient.DefaultRequestHeaders.Authorization = string.IsNullOrEmpty(_accessToken)
            ? null
            : new AuthenticationHeaderValue("Bearer", _accessToken);
        return true;
    }

    public async Task<AsgEventDtoPagedResult?> SearchEventsAsync(string query, int page = 1, int pageSize = 12)
    {
        var url = BuildUri("/api/Events/search", $"query={Uri.EscapeDataString(query ?? string.Empty)}&page={page}&pageSize={pageSize}");
        var resp = await _httpClient.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return null;
        var json = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<AsgEventDtoPagedResult>(json);
    }

    public async Task<IReadOnlyList<AsgMatchDto>?> GetMatchesByEventAsync(Guid eventId, int page = 1, int pageSize = 50, int? groupIndex = null, string? groupLabel = null)
    {
        var q = new List<string>
        {
            $"eventId={eventId}",
            $"page={page}",
            $"pageSize={pageSize}"
        };
        if (groupIndex.HasValue) q.Add($"groupIndex={groupIndex.Value}");
        if (!string.IsNullOrEmpty(groupLabel)) q.Add($"groupLabel={Uri.EscapeDataString(groupLabel)}");
        var url = BuildUri("/api/Matches", string.Join('&', q));
        var resp = await _httpClient.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return null;
        var json = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<AsgMatchDto[]>(json);
    }

    public async Task<AsgTeamDto?> GetTeamAsync(Guid teamId)
    {
        var url = BuildUri($"/api/Teams/{teamId}");
        var resp = await _httpClient.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return null;
        var json = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<AsgTeamDto>(json);
    }
}
