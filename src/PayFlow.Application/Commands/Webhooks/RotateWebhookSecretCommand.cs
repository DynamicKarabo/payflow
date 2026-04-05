using FluentValidation;
using MediatR;
using PayFlow.Application.DTOs;
using PayFlow.Application.Interfaces;
using PayFlow.Domain.Exceptions;
using PayFlow.Domain.ValueObjects;

namespace PayFlow.Application.Commands.Webhooks;

public sealed record RotateWebhookSecretCommand(
    string EndpointId,
    string NewSecret) : IRequest<WebhookEndpointResponse>;

public sealed class RotateWebhookSecretCommandValidator : AbstractValidator<RotateWebhookSecretCommand>
{
    public RotateWebhookSecretCommandValidator()
    {
        RuleFor(x => x.EndpointId)
            .NotEmpty()
            .WithMessage("Endpoint ID is required");

        RuleFor(x => x.NewSecret)
            .NotEmpty()
            .WithMessage("New secret is required")
            .MinimumLength(16)
            .WithMessage("New secret must be at least 16 characters");
    }
}

public sealed class RotateWebhookSecretCommandHandler : IRequestHandler<RotateWebhookSecretCommand, WebhookEndpointResponse>
{
    private readonly IWebhookEndpointRepository _webhookEndpointRepository;
    private readonly ITenantContext _tenantContext;

    public RotateWebhookSecretCommandHandler(
        IWebhookEndpointRepository webhookEndpointRepository,
        ITenantContext tenantContext)
    {
        _webhookEndpointRepository = webhookEndpointRepository;
        _tenantContext = tenantContext;
    }

    public async Task<WebhookEndpointResponse> Handle(RotateWebhookSecretCommand request, CancellationToken ct)
    {
        if (!Guid.TryParse(request.EndpointId, out var endpointGuid))
        {
            throw new WebhookEndpointNotFoundException();
        }

        var endpointId = new WebhookEndpointId(endpointGuid);
        var endpoint = await _webhookEndpointRepository.GetByIdAsync(endpointId, ct);

        if (endpoint == null)
        {
            throw new WebhookEndpointNotFoundException();
        }

        endpoint.RotateSecret(request.NewSecret);
        await _webhookEndpointRepository.UpdateAsync(endpoint, ct);

        return WebhookEndpointResponse.FromWebhookEndpoint(endpoint);
    }
}