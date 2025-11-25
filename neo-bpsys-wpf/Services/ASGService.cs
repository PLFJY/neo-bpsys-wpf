using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using neo_bpsys_wpf.Core.Abstractions.Services;
using System.Threading.Tasks;

namespace neo_bpsys_wpf.Services;

public class ASGService : IASGService
{
    private readonly HttpClient _httpClient;
    private readonly CookieContainer _cookies = new();
    private string? _accessToken;
    private readonly ISettingsHostService _settingsHostService;
    private string? _email;
    private string? _password;

    public ASGService(ISettingsHostService settingsHostService)
    {
        _settingsHostService = settingsHostService;
        var handler = new HttpClientHandler
        {
            UseCookies = true,
            CookieContainer = _cookies,
            AutomaticDecompression = DecompressionMethods.All
        };
        _httpClient = new HttpClient(handler);

        _email = _settingsHostService.Settings.AsgEmail;
        _password = _settingsHostService.Settings.AsgPassword;
        _settingsHostService.SettingsChanged += (_, _) =>
        {
            _email = _settingsHostService.Settings.AsgEmail;
            _password = _settingsHostService.Settings.AsgPassword;
        };
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

    private async Task EnsureLoginAsync()
    {
        if (!string.IsNullOrEmpty(_accessToken)) return;
        if (string.IsNullOrEmpty(_email) || string.IsNullOrEmpty(_password)) return;
        await LoginAsync(_email!, _password!);
    }

    private async Task<HttpResponseMessage> GetWithAuthAsync(Uri url)
    {
        await EnsureLoginAsync();
        var resp = await _httpClient.GetAsync(url);
        if (resp.StatusCode == HttpStatusCode.Unauthorized && !string.IsNullOrEmpty(_email) && !string.IsNullOrEmpty(_password))
        {
            resp.Dispose();
            var ok = await LoginAsync(_email!, _password!);
            if (ok)
            {
                resp = await _httpClient.GetAsync(url);
            }
        }
        return resp;
    }

    private async Task<HttpResponseMessage> PostWithAuthAsync(Uri url, HttpContent content)
    {
        await EnsureLoginAsync();
        var resp = await _httpClient.PostAsync(url, content);
        if (resp.StatusCode == HttpStatusCode.Unauthorized && !string.IsNullOrEmpty(_email) && !string.IsNullOrEmpty(_password))
        {
            resp.Dispose();
            var ok = await LoginAsync(_email!, _password!);
            if (ok)
            {
                resp = await _httpClient.PostAsync(url, content);
            }
        }
        return resp;
    }

    private async Task<HttpResponseMessage> PutWithAuthAsync(Uri url, HttpContent content)
    {
        await EnsureLoginAsync();
        var resp = await _httpClient.PutAsync(url, content);
        if (resp.StatusCode == HttpStatusCode.Unauthorized && !string.IsNullOrEmpty(_email) && !string.IsNullOrEmpty(_password))
        {
            resp.Dispose();
            var ok = await LoginAsync(_email!, _password!);
            if (ok)
            {
                resp = await _httpClient.PutAsync(url, content);
            }
        }
        return resp;
    }

    public async Task<bool> LoginAsync(string email, string password)
    {
        _email = email;
        _password = password;
        var body = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            { "email", email },
            { "password", password }
        });
        var resp = await _httpClient.PostAsync(BuildUri("/api/Auth/login"), new StringContent(body, Encoding.UTF8, "application/json"));
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
        var resp = await GetWithAuthAsync(url);
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
        var resp = await GetWithAuthAsync(url);
        if (!resp.IsSuccessStatusCode) return null;
        var json = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<AsgMatchDto[]>(json);
    }

    public async Task<AsgTeamDto?> GetTeamAsync(Guid teamId)
    {
        var url = BuildUri($"/api/Teams/{teamId}");
        var resp = await GetWithAuthAsync(url);
        if (!resp.IsSuccessStatusCode) return null;
        var json = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<AsgTeamDto>(json);
    }

    public async Task<bool> UpdateMatchScoresAsync(Guid matchId, AsgMatchScoresUpdateDto payload)
    {
        var url = BuildUri($"/api/Matches/{matchId}/scores");
        var body = JsonSerializer.Serialize(payload);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        var resp = await PutWithAuthAsync(url, content);
        return resp.IsSuccessStatusCode;
    }
}
