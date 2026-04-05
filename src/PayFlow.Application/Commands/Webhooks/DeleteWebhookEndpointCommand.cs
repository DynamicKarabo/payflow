using MediatR;
using PayFlow.Application.Interfaces;
using PayFlow.Domain.ValueObjects;

namespace PayFlow.Application.Commands.Webhooks;

public sealed record DeleteWebhookEndpointCommand(string EndpointId) : IRequest<bool>;

public sealed class DeleteWebhookEndpointCommandHandler : IRequestHandler<DeleteWebhookEndpointCommand, bool>
{
    private readonly IWebhookEndpointRepository _webhookEndpointRepository;
    private readonly ITenantContext _tenantContext;

    public DeleteWebhookEndpointCommandHandler(
        IWebhookEndpointRepository webhookEndpointRepository,
        ITenantContext tenantContext)
    {
        _webhookEndpointRepository = webhookEndpointRepository;
        _tenantContext = tenantContext;
    }

    public async Task<bool> Handle(DeleteWebhookEndpointCommand request, CancellationToken ct)
    {
        if (!Guid.TryParse(request.EndpointId, out var endpointGuid))
        {
            return false;
        }

        var endpointId = new WebhookEndpointId(endpointGuid);
        var endpoint = await _webhookEndpointRepository.GetByIdAsync(endpointId, ct);

        if (endpoint == null)
        {
            return false;
        }

        await _webhookEndpointRepository.DeleteAsync(endpointId, ct);
        return true;
    }
}