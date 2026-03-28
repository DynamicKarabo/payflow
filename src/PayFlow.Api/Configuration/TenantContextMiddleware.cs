using PayFlow.Application.Interfaces;
using PayFlow.Domain.Enums;
using PayFlow.Domain.ValueObjects;

namespace PayFlow.Api.Configuration;

public class TenantContextData
{
    public TenantId TenantId { get; set; }
    public PaymentMode Mode { get; set; }
    public bool IsLive => Mode == PaymentMode.Live;
}

public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContextService tenantContextService)
    {
        if (context.Items.TryGetValue("TenantContext", out var tenantContextObj) && tenantContextObj is TenantContextData tenantContext)
        {
            tenantContextService.SetTenantContext(tenantContext);
        }

        await _next(context);
    }
}

public interface ITenantContextService
{
    TenantContextData? GetCurrentContext();
    void SetTenantContext(TenantContextData context);
}

public class TenantContextServiceImpl : ITenantContextService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantContextServiceImpl(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public TenantContextData? GetCurrentContext()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context?.Items.TryGetValue("TenantContext", out var obj) == true && obj is TenantContextData tc)
        {
            return tc;
        }
        return null;
    }

    public void SetTenantContext(TenantContextData context)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        httpContext?.Items.Add("TenantContext", context);
    }
}
