using FluentValidation;
using Hangfire;
using Hangfire.SqlServer;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PayFlow.Api.Configuration;
using PayFlow.Api.Endpoints;
using PayFlow.Api.Middleware;
using PayFlow.Application.Behaviors;
using PayFlow.Application.Commands;
using PayFlow.Application.Interfaces;
using PayFlow.Domain.Enums;
using PayFlow.Domain.ValueObjects;
using PayFlow.Infrastructure.Persistence.Context;
using PayFlow.Infrastructure.Redis;
using PayFlow.Infrastructure.ServiceBus;
using PayFlow.Infrastructure.Signing;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContextService, TenantContextServiceImpl>();
builder.Services.AddScoped<PayFlow.Application.Interfaces.ITenantContext>(sp =>
{
    var service = sp.GetRequiredService<ITenantContextService>();
    var ctx = service.GetCurrentContext();
    return new TenantContextWrapper(ctx);
});

builder.Services.AddDbContextFactory<PayFlowDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlServer(connectionString);
});

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = ConfigurationOptions.Parse(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379");
    configuration.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(configuration);
});

builder.Services.AddScoped<IIdempotencyService, RedisIdempotencyService>();
builder.Services.AddScoped<IWebhookSigner, HmacWebhookSigner>();

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(CreatePaymentCommand).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
});

builder.Services.AddValidatorsFromAssembly(typeof(CreatePaymentCommand).Assembly);

builder.Services.AddHttpClient("WebhookClient");

builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection"), new SqlServerStorageOptions
    {
        SchemaName = "HangfireSchema",
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        UseRecommendedIsolationLevel = true,
        DisableGlobalLocks = true
    }));

builder.Services.AddHangfireServer(options =>
{
    options.Queues = new[] { "webhook", "refund", "settlement", "default" };
    options.WorkerCount = Environment.ProcessorCount * 2;
});

var app = builder.Build();

app.UseErrorHandling();
app.UseApiKeyAuthentication();

app.MapPaymentsEndpoints();

app.MapHangfireDashboard("/admin/hangfire");

app.MapGet("/health/ready", async (HttpContext context) =>
{
    var dbFactory = context.RequestServices.GetRequiredService<IDbContextFactory<PayFlowDbContext>>();
    try
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.Database.CanConnectAsync();
        return Results.Ok(new { status = "healthy" });
    }
    catch
    {
        return Results.Json(new { status = "unhealthy" }, statusCode: 503);
    }
});

app.MapGet("/health/live", () => Results.Ok(new { status = "alive" }));

app.Run();

public class TenantContextWrapper : PayFlow.Application.Interfaces.ITenantContext
{
    private readonly TenantContextData? _context;

    public TenantContextWrapper(TenantContextData? context)
    {
        _context = context;
    }

    public TenantId TenantId => _context?.TenantId ?? throw new InvalidOperationException("No tenant context");
    public PaymentMode Mode => _context?.Mode ?? PaymentMode.Test;
    public bool IsLive => Mode == PaymentMode.Live;
}
