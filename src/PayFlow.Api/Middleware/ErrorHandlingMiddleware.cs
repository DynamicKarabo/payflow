using System.Net;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using PayFlow.Application.Commands;
using PayFlow.Domain.Exceptions;

namespace PayFlow.Api.Middleware;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        _logger.LogError(exception, "An unhandled exception occurred");

        var response = context.Response;
        response.ContentType = "application/problem+json";

        var (statusCode, title, detail, extensions) = MapException(exception);

        response.StatusCode = statusCode;

        var problemDetails = new ProblemDetails
        {
            Type = $"https://payflow.io/errors/{title.ToLowerInvariant().Replace(" ", "-")}",
            Title = title,
            Status = statusCode,
            Detail = detail,
            Extensions = extensions
        };

        var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await response.WriteAsync(json);
    }

    private static (int statusCode, string title, string detail, Dictionary<string, object?> extensions) MapException(Exception ex)
    {
        return ex switch
        {
            ValidationException ve => (
                StatusCode: 422,
                Title: "Validation Error",
                Detail: "One or more validation errors occurred.",
                Extensions: new Dictionary<string, object?>
                {
                    ["errors"] = ve.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage })
                }),

            PaymentInFlightException => (
                StatusCode: 409,
                Title: "Payment In Flight",
                Detail: "A payment with this idempotency key is already being processed.",
                Extensions: new Dictionary<string, object?> { ["type"] = "payment_in_flight" }),

            IdempotencyConflictException => (
                StatusCode: 422,
                Title: "Idempotency Conflict",
                Detail: "The request body does not match the original request.",
                Extensions: new Dictionary<string, object?> { ["type"] = "idempotency_conflict" }),

            InvalidPaymentTransitionException ipt => (
                StatusCode: 409,
                Title: "Invalid Payment State",
                Detail: $"Cannot perform this operation on a payment in '{ipt.From}' state.",
                Extensions: new Dictionary<string, object?>
                {
                    ["type"] = "invalid_payment_state",
                    ["current_state"] = ipt.From.ToString()
                }),

            InsufficientRefundableAmountException ir => (
                StatusCode: 422,
                Title: "Insufficient Refundable Amount",
                Detail: $"Refund amount exceeds available refundable amount.",
                Extensions: new Dictionary<string, object?>
                {
                    ["type"] = "insufficient_refundable_amount",
                    ["requested"] = ir.Requested.Amount,
                    ["available"] = ir.Available.Amount
                }),

            PaymentNotFoundException => (
                StatusCode: 404,
                Title: "Not Found",
                Detail: "The requested payment was not found.",
                Extensions: new Dictionary<string, object?> { ["type"] = "not_found" }),

            DomainException de => (
                StatusCode: 400,
                Title: "Bad Request",
                Detail: de.Message,
                Extensions: new Dictionary<string, object?>()),

            _ => (
                StatusCode: 500,
                Title: "Internal Server Error",
                Detail: "An unexpected error occurred.",
                Extensions: new Dictionary<string, object?>())
        };
    }
}

public static class ErrorHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseErrorHandling(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ErrorHandlingMiddleware>();
    }
}
