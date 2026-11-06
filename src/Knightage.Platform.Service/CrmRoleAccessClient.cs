using System.Net.Http.Json;
using System.Text.Json;
using Knightage.Platform.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Knightage.Platform.Service;

public class CrmRoleAccessClient : ICrmRoleAccessClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CrmRoleAccessClient> _logger;

    public CrmRoleAccessClient(IHttpClientFactory httpClientFactory, ILogger<CrmRoleAccessClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, bool>>?> GetRoleScreensAsync(string? authorizationHeader, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("Crm");
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/screen-access");
            if (!string.IsNullOrEmpty(authorizationHeader))
            {
                request.Headers.TryAddWithoutValidation("Authorization", authorizationHeader);
            }

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("CRM screen access returned {StatusCode}; the Users screen shows CRM's built-in role defaults.", response.StatusCode);
                return null;
            }

            var body = await response.Content.ReadFromJsonAsync<MatrixResponse>(JsonOptions, cancellationToken);
            if (body?.Roles is null || body.Screens is null)
            {
                return null;
            }

            return body.Roles.ToDictionary(
                role => role,
                role => (IReadOnlyDictionary<string, bool>)body.Screens
                    .Where(s => s.Key is not null && s.Allowed is not null && s.Allowed.ContainsKey(role))
                    .ToDictionary(s => s.Key!, s => s.Allowed![role]));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read CRM's screen access; the Users screen shows CRM's built-in role defaults.");
            return null;
        }
    }

    private record MatrixResponse(List<string>? Roles, List<ScreenRow>? Screens);
    private record ScreenRow(string? Key, Dictionary<string, bool>? Allowed);
}
