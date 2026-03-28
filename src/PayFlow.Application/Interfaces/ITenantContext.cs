using PayFlow.Domain.Enums;
using PayFlow.Domain.ValueObjects;

namespace PayFlow.Application.Interfaces;

public interface ITenantContext
{
    TenantId TenantId { get; }
    PaymentMode Mode { get; }
    bool IsLive => Mode == PaymentMode.Live;
}
