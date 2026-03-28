using MediatR;
using PayFlow.Application.Commands;
using PayFlow.Application.DTOs;

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
            new PaymentMethodRequest(
                request.PaymentMethod.Type,
                request.PaymentMethod.Token,
                request.PaymentMethod.Last4,
                request.PaymentMethod.Brand,
                request.PaymentMethod.ExpiryMonth,
                request.PaymentMethod.ExpiryYear),
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
        return Results.Ok(new { id, status = "placeholder" });
    }
}

public sealed record CreatePaymentRequest(
    long Amount,
    string Currency,
    string CustomerId,
    PaymentMethodRequest PaymentMethod,
    bool AutoCapture,
    Dictionary<string, string>? Metadata);

public sealed record PaymentMethodRequest(
    string Type,
    string? Token,
    string? Last4,
    string? Brand,
    string? ExpiryMonth,
    string? ExpiryYear);
