using MediatR;
using PayFlow.Application.DTOs;
using PayFlow.Application.Interfaces;

namespace PayFlow.Application.Queries;

public sealed record GetWebhookEndpointsQuery : IRequest<IReadOnlyList<WebhookEndpointResponse>>;

public sealed class GetWebhookEndpointsQueryHandler : IRequestHandler<GetWebhookEndpointsQuery, IReadOnlyList<WebhookEndpointResponse>>
{
    private readonly IWebhookEndpointRepository _webhookEndpointRepository;
    private readonly ITenantContext _tenantContext;

    public GetWebhookEndpointsQueryHandler(
        IWebhookEndpointRepository webhookEndpointRepository,
        ITenantContext tenantContext)
    {
        _webhookEndpointRepository = webhookEndpointRepository;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<WebhookEndpointResponse>> Handle(GetWebhookEndpointsQuery request, CancellationToken ct)
    {
        var endpoints = await _webhookEndpointRepository.GetByTenantAsync(_tenantContext.TenantId, ct);
        return endpoints.Select(WebhookEndpointResponse.FromWebhookEndpoint).ToList().AsReadOnly();
    }
}