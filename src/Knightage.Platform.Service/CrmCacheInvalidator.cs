using Knightage.Platform.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Knightage.Platform.Service;

public class CrmCacheInvalidator : ICrmCacheInvalidator
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CrmCacheInvalidator> _logger;

    public CrmCacheInvalidator(IHttpClientFactory httpClientFactory, ILogger<CrmCacheInvalidator> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task InvalidateAsync(Guid organizationId, string? authorizationHeader, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("Crm");
            var request = new HttpRequestMessage(HttpMethod.Post, $"api/internal/tenant-cache/{organizationId}/invalidate");
            if (!string.IsNullOrEmpty(authorizationHeader))
            {
                request.Headers.TryAddWithoutValidation("Authorization", authorizationHeader);
            }

            var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("CRM cache invalidation for organization {OrganizationId} returned {StatusCode}.", organizationId, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            // Best-effort by design -- see ICrmCacheInvalidator. CRM will still end up
            // correct once its own cache entry expires; this only shortens that window.
            _logger.LogWarning(ex, "CRM cache invalidation failed for organization {OrganizationId}.", organizationId);
        }
    }
}
