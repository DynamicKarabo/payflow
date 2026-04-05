using MediatR;
using PayFlow.Application.Commands.Webhooks;
using PayFlow.Application.Queries;

namespace PayFlow.Api.Endpoints;

public static class WebhookEndpointsEndpoint
{
    public static void MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/webhook-endpoints");

        group.MapPost("/", CreateWebhookEndpoint)
            .WithName("CreateWebhookEndpoint")
            .WithTags("Webhooks");

        group.MapGet("/", GetWebhookEndpoints)
            .WithName("GetWebhookEndpoints")
            .WithTags("Webhooks");

        group.MapPut("/{id}", UpdateWebhookEndpoint)
            .WithName("UpdateWebhookEndpoint")
            .WithTags("Webhooks");

        group.MapDelete("/{id}", DeleteWebhookEndpoint)
            .WithName("DeleteWebhookEndpoint")
            .WithTags("Webhooks");

        group.MapPost("/{id}/rotate-secret", RotateWebhookSecret)
            .WithName("RotateWebhookSecret")
            .WithTags("Webhooks");
    }

    private static async Task<IResult> CreateWebhookEndpoint(
        CreateWebhookEndpointRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new CreateWebhookEndpointCommand(request.Url, request.Secret, request.EventTypes);
        var result = await mediator.Send(command, ct);
        return Results.Created($"/v1/webhook-endpoints/{result.Id}", result);
    }

    private static async Task<IResult> GetWebhookEndpoints(
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetWebhookEndpointsQuery();
        var result = await mediator.Send(query, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> UpdateWebhookEndpoint(
        string id,
        UpdateWebhookEndpointRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new UpdateWebhookEndpointCommand(id, request.Url, request.EventTypes);
        var result = await mediator.Send(command, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> DeleteWebhookEndpoint(
        string id,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new DeleteWebhookEndpointCommand(id);
        var deleted = await mediator.Send(command, ct);
        return deleted ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> RotateWebhookSecret(
        string id,
        RotateWebhookSecretRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new RotateWebhookSecretCommand(id, request.NewSecret);
        var result = await mediator.Send(command, ct);
        return Results.Ok(result);
    }
}

public sealed record CreateWebhookEndpointRequest(
    string Url,
    string Secret,
    IEnumerable<string> EventTypes);

public sealed record UpdateWebhookEndpointRequest(
    string? Url,
    IEnumerable<string>? EventTypes);

public sealed record RotateWebhookSecretRequest(string NewSecret);