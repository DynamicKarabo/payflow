using MediatR;
using PayFlow.Application.Commands;
using PayFlow.Application.DTOs;
using PayFlow.Application.Queries;

namespace PayFlow.Api.Endpoints;

public static class PaymentsEndpoint
{
    public static void MapPaymentsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/payments");

        group.MapPost("/", CreatePayment)
            .WithName("CreatePayment")
            .WithTags("Payments");

        group.MapGet("/{id}", GetPayment)
            .WithName("GetPayment")
            .WithTags("Payments");

        group.MapPost("/{id}/capture", CapturePayment)
            .WithName("CapturePayment")
            .WithTags("Payments");

        group.MapPost("/{id}/refund", RefundPayment)
            .WithName("RefundPayment")
            .WithTags("Payments");

        group.MapPost("/{id}/cancel", CancelPayment)
            .WithName("CancelPayment")
            .WithTags("Payments");

        group.MapPost("/{id}/fail", FailPayment)
            .WithName("FailPayment")
            .WithTags("Payments");
    }

    private static async Task<IResult> CreatePayment(
        CreatePaymentRequest request,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        var idempotencyKeyHeader = context.Request.Headers["Idempotency-Key"].FirstOrDefault();
        
        var metadata = request.Metadata ?? new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(idempotencyKeyHeader))
        {
            if (idempotencyKeyHeader.Length > 64 || !System.Text.RegularExpressions.Regex.IsMatch(idempotencyKeyHeader, @"^[a-zA-Z0-9\-]+$"))
            {
                return Results.BadRequest(new { error = "invalid_idempotency_key", detail = "Idempotency-Key must be alphanumeric with hyphens, max 64 chars" });
            }
            metadata["idempotency_key"] = idempotencyKeyHeader;
        }
        else
        {
            metadata["idempotency_key"] = Guid.NewGuid().ToString();
        }

        var command = new CreatePaymentCommand(
            request.Amount,
            request.Currency,
            request.CustomerId,
            request.PaymentMethod,
            request.AutoCapture,
            metadata);

        var result = await mediator.Send(command, ct);

        return Results.Created($"/v1/payments/{result.Id}", result);
    }

    private static async Task<IResult> GetPayment(
        string id,
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetPaymentQuery(id);
        var result = await mediator.Send(query, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> CapturePayment(
        string id,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new CapturePaymentCommand(id);
        var result = await mediator.Send(command, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> RefundPayment(
        string id,
        RefundPaymentRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new RefundPaymentCommand(id, request.Amount, request.Currency, request.Reason);
        var result = await mediator.Send(command, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> CancelPayment(
        string id,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new CancelPaymentCommand(id);
        var result = await mediator.Send(command, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> FailPayment(
        string id,
        FailPaymentRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new FailPaymentCommand(id, request.Reason);
        var result = await mediator.Send(command, ct);
        return Results.Ok(result);
    }
}

public sealed record CreatePaymentRequest(
    long Amount,
    string Currency,
    string CustomerId,
    PaymentMethodRequest PaymentMethod,
    bool AutoCapture,
    Dictionary<string, string>? Metadata);

public sealed record RefundPaymentRequest(
    decimal Amount,
    string Currency,
    string Reason);

public sealed record FailPaymentRequest(string Reason);
