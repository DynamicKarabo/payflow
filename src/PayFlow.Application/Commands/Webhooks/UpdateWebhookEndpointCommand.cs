using FluentValidation;
using MediatR;
using PayFlow.Application.DTOs;
using PayFlow.Application.Interfaces;
using PayFlow.Domain.Exceptions;
using PayFlow.Domain.ValueObjects;

namespace PayFlow.Application.Commands.Webhooks;

public sealed record UpdateWebhookEndpointCommand(
    string EndpointId,
    string? Url,
    IEnumerable<string>? EventTypes) : IRequest<WebhookEndpointResponse>;

public sealed class UpdateWebhookEndpointCommandValidator : AbstractValidator<UpdateWebhookEndpointCommand>
{
    public UpdateWebhookEndpointCommandValidator()
    {
        RuleFor(x => x.EndpointId)
            .NotEmpty()
            .WithMessage("Endpoint ID is required");

        RuleFor(x => x)
            .Must(x => x.Url != null || x.EventTypes != null)
            .WithMessage("At least one field must be provided for update");

        When(x => x.Url != null, () =>
        {
            RuleFor(x => x.Url!)
                .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https")
                .WithMessage("URL must be a valid HTTPS URL");
        });
    }
}

public sealed class UpdateWebhookEndpointCommandHandler : IRequestHandler<UpdateWebhookEndpointCommand, WebhookEndpointResponse>
{
    private readonly IWebhookEndpointRepository _webhookEndpointRepository;
    private readonly ITenantContext _tenantContext;

    public UpdateWebhookEndpointCommandHandler(
        IWebhookEndpointRepository webhookEndpointRepository,
        ITenantContext tenantContext)
    {
        _webhookEndpointRepository = webhookEndpointRepository;
        _tenantContext = tenantContext;
    }

    public async Task<WebhookEndpointResponse> Handle(UpdateWebhookEndpointCommand request, CancellationToken ct)
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

        if (request.Url != null)
        {
            endpoint.UpdateUrl(request.Url);
        }

        if (request.EventTypes != null)
        {
            endpoint.UpdateEventTypes(request.EventTypes);
        }

        await _webhookEndpointRepository.UpdateAsync(endpoint, ct);

        return WebhookEndpointResponse.FromWebhookEndpoint(endpoint);
    }
}

public class WebhookEndpointNotFoundException : DomainException
{
    public WebhookEndpointNotFoundException() : base("Webhook endpoint not found") { }
}