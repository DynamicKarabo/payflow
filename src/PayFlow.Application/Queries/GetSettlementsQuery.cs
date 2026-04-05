using MediatR;
using PayFlow.Application.DTOs;
using PayFlow.Application.Interfaces;

namespace PayFlow.Application.Queries;

public sealed record GetSettlementsQuery(
    string? FromDate = null,
    string? ToDate = null) : IRequest<IReadOnlyList<SettlementBatchResponse>>;

public sealed class GetSettlementsQueryHandler : IRequestHandler<GetSettlementsQuery, IReadOnlyList<SettlementBatchResponse>>
{
    private readonly ISettlementBatchRepository _settlementBatchRepository;
    private readonly ITenantContext _tenantContext;

    public GetSettlementsQueryHandler(
        ISettlementBatchRepository settlementBatchRepository,
        ITenantContext tenantContext)
    {
        _settlementBatchRepository = settlementBatchRepository;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<SettlementBatchResponse>> Handle(GetSettlementsQuery request, CancellationToken ct)
    {
        DateOnly? fromDate = null;
        DateOnly? toDate = null;

        if (!string.IsNullOrEmpty(request.FromDate) && DateOnly.TryParse(request.FromDate, out var parsedFromDate))
        {
            fromDate = parsedFromDate;
        }

        if (!string.IsNullOrEmpty(request.ToDate) && DateOnly.TryParse(request.ToDate, out var parsedToDate))
        {
            toDate = parsedToDate;
        }

        var batches = await _settlementBatchRepository.GetByTenantAsync(
            _tenantContext.TenantId,
            fromDate,
            toDate,
            ct);

        return batches.Select(SettlementBatchResponse.FromSettlementBatch).ToList().AsReadOnly();
    }
}