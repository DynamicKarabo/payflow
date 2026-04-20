using Microsoft.EntityFrameworkCore;
using PayFlow.Domain.Enums;
using PayFlow.Infrastructure.Persistence.Context;

namespace PayFlow.Api.Endpoints;

public static class DashboardEndpoint
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/dashboard");

        group.MapGet("/stats", GetDashboardStats)
            .WithName("GetDashboardStats")
            .WithTags("Dashboard");
    }

    private static async Task<IResult> GetDashboardStats(
        PayFlowDbContext db,
        CancellationToken ct)
    {
        var totalPayments = await db.Payments.LongCountAsync(ct);

        var totalAmount = totalPayments > 0
            ? await db.Payments.SumAsync(p => p.Amount.Amount, ct)
            : 0m;

        var settledCount = await db.Payments
            .LongCountAsync(p => p.Status == PaymentStatus.Settled, ct);

        var successRate = totalPayments > 0
            ? (double)settledCount / totalPayments * 100.0
            : 0.0;

        var pendingSettlements = await db.Payments
            .LongCountAsync(p => p.Status == PaymentStatus.Captured, ct);

        var response = new DashboardStatsResponse(
            TotalPayments: totalPayments,
            TotalAmount: totalAmount,
            SuccessRate: successRate,
            PendingSettlements: pendingSettlements);

        return Results.Ok(response);
    }
}

public sealed record DashboardStatsResponse(
    long TotalPayments,
    decimal TotalAmount,
    double SuccessRate,
    long PendingSettlements);
