using MediatR;
using PayFlow.Application.Queries;

namespace PayFlow.Api.Endpoints;

public static class SettlementsEndpoint
{
    public static void MapSettlementsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/settlements");

        group.MapGet("/", GetSettlements)
            .WithName("GetSettlements")
            .WithTags("Settlements");

        group.MapGet("/{id}", GetSettlementById)
            .WithName("GetSettlementById")
            .WithTags("Settlements");
    }

    private static async Task<IResult> GetSettlements(
        IMediator mediator,
        string? fromDate,
        string? toDate,
        CancellationToken ct)
    {
        var query = new GetSettlementsQuery(fromDate, toDate);
        var result = await mediator.Send(query, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetSettlementById(
        string id,
        IMediator mediator,
        CancellationToken ct)
    {
        // For now, return a placeholder - would need a GetSettlementByIdQuery
        return Results.Ok(new { id, status = "placeholder" });
    }
}