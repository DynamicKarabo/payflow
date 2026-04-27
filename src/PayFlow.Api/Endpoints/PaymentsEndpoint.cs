using MediatR;
using PayFlow.Application.Commands;
using PayFlow.Application.DTOs;
using PayFlow.Application.Queries;
using StackExchange.Redis;

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
        IConnectionMultiplexer redis,
        CancellationToken ct)
    {
        var query = new GetPaymentQuery(id);
        var result = await mediator.Send(query, ct);

        // attempt to extract raw guid from id like "pay_{guid}"
        var rawId = result.Id != null && result.Id.StartsWith("pay_") ? result.Id.Substring(4) : result.Id;
        double? fraudScore = null;
        if (!string.IsNullOrEmpty(rawId))
        {
            var db = redis.GetDatabase();
            var scoreValue = await db.StringGetAsync($"fraud:payment:{rawId}");
            if (scoreValue.HasValue && double.TryParse((string)scoreValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            {
                fraudScore = parsed;
            }
        }

        var responseWithFraud = result with { FraudScore = fraudScore };
        return Results.Ok(responseWithFraud);
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
