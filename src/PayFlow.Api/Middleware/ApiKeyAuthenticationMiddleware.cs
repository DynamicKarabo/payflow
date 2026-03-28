using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PayFlow.Api.Configuration;
using PayFlow.Domain.Entities;
using PayFlow.Domain.Entities;
using PayFlow.Domain.Enums;
using PayFlow.Domain.ValueObjects;
using PayFlow.Infrastructure.Persistence.Context;

namespace PayFlow.Api.Middleware;

public class ApiKeyAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;
    private static readonly Regex ApiKeyPattern = new(@"^(pk_live_|pk_test_)[a-zA-Z0-9]+$", RegexOptions.Compiled);

    public ApiKeyAuthenticationMiddleware(RequestDelegate next, ILogger<ApiKeyAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IDbContextFactory<PayFlowDbContext> dbFactory)
    {
        var path = context.Request.Path.Value ?? "";

        if (path.StartsWith("/health") || path.StartsWith("/admin/hangfire"))
        {
            await _next(context);
            return;
        }

        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "missing_authorization" });
            return;
        }

        var apiKey = authHeader["Bearer ".Length..].Trim();
        
        if (string.IsNullOrEmpty(apiKey) || !ApiKeyPattern.IsMatch(apiKey))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "invalid_api_key_format" });
            return;
        }

        var keyPrefix = apiKey.Length >= 12 ? apiKey[..12] : apiKey;

        await using var db = await dbFactory.CreateDbContextAsync(context.RequestAborted);
        var apiKeyEntity = await db.ApiKeys
            .FirstOrDefaultAsync(k => k.KeyPrefix == keyPrefix && k.Status == ApiKeyStatus.Active, context.RequestAborted);

        if (apiKeyEntity == null)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "invalid_api_key" });
            return;
        }

        if (apiKeyEntity.IsExpired())
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "api_key_expired" });
            return;
        }

        var tenant = await db.Tenants
            .FirstOrDefaultAsync(t => t.Id == apiKeyEntity.TenantId, context.RequestAborted);

        if (tenant == null || tenant.Status == TenantStatus.Closed)
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new { error = "tenant_not_active" });
            return;
        }

        if (tenant.Status == TenantStatus.Suspended && !IsReadOnlyPath(path, context.Request.Method))
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new { error = "tenant_suspended" });
            return;
        }

        var tenantContext = new TenantContextData
        {
            TenantId = apiKeyEntity.TenantId,
            Mode = apiKeyEntity.Mode
        };

        context.Items["TenantContext"] = tenantContext;

        await _next(context);
    }

    private static bool IsReadOnlyPath(string path, string method)
    {
        return path.StartsWith("/v1/payments/") && HttpMethods.IsGet(method);
    }
}

public static class ApiKeyAuthenticationMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiKeyAuthenticationMiddleware>();
    }
}
