using PayFlow.Application.Interfaces;
using PayFlow.Domain.Enums;
using PayFlow.Domain.ValueObjects;
using Microsoft.Extensions.Hosting;

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
    private readonly IHostEnvironment _env;

    public TenantContextServiceImpl(IHttpContextAccessor httpContextAccessor, IHostEnvironment env)
    {
        _httpContextAccessor = httpContextAccessor;
        _env = env;
    }

    public TenantContextData? GetCurrentContext()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context?.Items.TryGetValue("TenantContext", out var obj) == true && obj is TenantContextData tc)
        {
            return tc;
        }

        // Development fallback: provide a default tenant context so health checks and
        // background operations can resolve scoped services without an HTTP request.
        if (_env.IsDevelopment())
        {
            return new TenantContextData
            {
                TenantId = new TenantId(Guid.Empty), // dummy tenant
                Mode = PaymentMode.Test
            };
        }

        return null;
    }

    public void SetTenantContext(TenantContextData context)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        httpContext?.Items.Add("TenantContext", context);
    }
}
