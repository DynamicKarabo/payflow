using FluentValidation;
using MediatR;
using PayFlow.Application.DTOs;
using PayFlow.Application.Interfaces;
using PayFlow.Domain.Entities;
using PayFlow.Domain.ValueObjects;

namespace PayFlow.Application.Commands.Webhooks;

public sealed record CreateWebhookEndpointCommand(
    string Url,
    string Secret,
    IEnumerable<string> EventTypes) : IRequest<WebhookEndpointResponse>;

public sealed class CreateWebhookEndpointCommandValidator : AbstractValidator<CreateWebhookEndpointCommand>
{
    public CreateWebhookEndpointCommandValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty()
            .WithMessage("URL is required")
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https")
            .WithMessage("URL must be a valid HTTPS URL");

        RuleFor(x => x.Secret)
            .NotEmpty()
            .WithMessage("Secret is required")
            .MinimumLength(16)
            .WithMessage("Secret must be at least 16 characters");

        RuleFor(x => x.EventTypes)
            .NotEmpty()
            .WithMessage("At least one event type is required")
            .Must(types => types.Any())
            .WithMessage("At least one event type is required");
    }
}

public sealed class CreateWebhookEndpointCommandHandler : IRequestHandler<CreateWebhookEndpointCommand, WebhookEndpointResponse>
{
    private readonly IWebhookEndpointRepository _webhookEndpointRepository;
    private readonly ITenantContext _tenantContext;

    public CreateWebhookEndpointCommandHandler(
        IWebhookEndpointRepository webhookEndpointRepository,
        ITenantContext tenantContext)
    {
        _webhookEndpointRepository = webhookEndpointRepository;
        _tenantContext = tenantContext;
    }

    public async Task<WebhookEndpointResponse> Handle(CreateWebhookEndpointCommand request, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId;

        var endpoint = WebhookEndpoint.Create(
            tenantId,
            request.Url,
            request.Secret,
            request.EventTypes);

        await _webhookEndpointRepository.AddAsync(endpoint, ct);

        return WebhookEndpointResponse.FromWebhookEndpoint(endpoint);
    }
}