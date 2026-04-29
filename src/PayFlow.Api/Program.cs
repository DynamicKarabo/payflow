using FluentValidation;
using Hangfire;
using Hangfire.SqlServer;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using PayFlow.Api.Configuration;
using PayFlow.Api.Endpoints;
using PayFlow.Api.Middleware;
using System.Threading.RateLimiting;
using PayFlow.Application.Behaviors;
using PayFlow.Application.Commands;
using PayFlow.Application.Interfaces;
using PayFlow.Domain.Enums;
using PayFlow.Domain.ValueObjects;
using PayFlow.Infrastructure.Gateways;
using PayFlow.Infrastructure.Persistence.Context;
using PayFlow.Infrastructure.Persistence.Repositories;
using PayFlow.Infrastructure.Redis;
using PayFlow.Infrastructure.ServiceBus;
using PayFlow.Infrastructure.Signing;
using PayFlow.Infrastructure.Dispatchers;
using StackExchange.Redis;
using PayFlow.Infrastructure.Fraud;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContextService, TenantContextServiceImpl>();
builder.Services.AddScoped<PayFlow.Application.Interfaces.ITenantContext>(sp =>
{
    var service = sp.GetRequiredService<ITenantContextService>();
    var ctx = service.GetCurrentContext();
    return new TenantContextWrapper(ctx);
});
builder.Services.AddScoped<PayFlow.Infrastructure.MultiTenancy.ITenantContext>(sp =>
    (PayFlow.Infrastructure.MultiTenancy.ITenantContext)sp.GetRequiredService<PayFlow.Application.Interfaces.ITenantContext>());

builder.Services.AddDbContextFactory<PayFlowDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlServer(connectionString);
});

builder.Services.AddDbContext<AdminDbContext>(options =>
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
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IPaymentGatewayAdapter>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    var logger = sp.GetRequiredService<ILogger<RealPaymentGatewayAdapter>>();
    var config = sp.GetRequiredService<IConfiguration>();
    var baseUrl = config.GetValue<string>("PaymentGateway:BaseUrl") ?? "http://localhost:5000";
    return new RealPaymentGatewayAdapter(httpClient, logger, baseUrl);
});
builder.Services.AddScoped<IWebhookEndpointRepository, WebhookEndpointRepository>();
builder.Services.AddScoped<ISettlementBatchRepository, SettlementBatchRepository>();

// Register domain event publisher
builder.Services.AddScoped<IDomainEventPublisher, DomainEventPublisher>();

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(CreatePaymentCommand).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
});

builder.Services.AddValidatorsFromAssembly(typeof(CreatePaymentCommand).Assembly);

builder.Services.AddHttpClient("WebhookClient");

// Fraud scoring service
builder.Services.AddHttpClient<PayFlow.Application.Fraud.IFraudScoringService, FraudScoringService>(client =>
{
    var serviceUrl = builder.Configuration.GetValue<string>("FraudScoring:ServiceUrl") ?? "http://localhost:8000";
    client.BaseAddress = new Uri(serviceUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

// CORS
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173" };
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Rate Limiting (per-IP)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
});

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PayFlow API",
        Version = "v1",
        Description = "A payment processing API"
    });
});

// Hangfire gate: single source of truth for all Hangfire-related registration
var hangfireConnStr = builder.Configuration["Hangfire:ConnectionString"];
var enableHangfire = !string.IsNullOrEmpty(hangfireConnStr) && 
                    (hangfireConnStr.Contains("Server=", StringComparison.OrdinalIgnoreCase) || 
                     hangfireConnStr.Contains("Data Source=", StringComparison.OrdinalIgnoreCase));
Console.WriteLine($"[STARTUP] Hangfire:ConnectionString = '{hangfireConnStr ?? "(null)"}' => enableHangfire={enableHangfire}");

// Hangfire: only register if connection string exists (skip in tests)
if (enableHangfire)
{
    builder.Services.AddHangfire(configuration => configuration
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseSqlServerStorage(builder.Configuration["Hangfire:ConnectionString"], new SqlServerStorageOptions
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

    // WebhookDispatcher requires Hangfire — only register in prod
    builder.Services.AddScoped<IWebhookDispatcher, WebhookDispatcher>();
}
else
{
    // Test environment: no-op webhook dispatcher (Hangfire disabled)
    builder.Services.AddScoped<IWebhookDispatcher, NoOpWebhookDispatcher>();
}

var app = builder.Build();

    // Auto-migrate database on startup (development/demo) — non-blocking
    Task.Run(async () =>
    {
        await Task.Delay(5000); // give services a moment
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayFlowDbContext>();
        var loggerFactory = scope.ServiceProvider.GetService<ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger("Startup") ?? 
                     scope.ServiceProvider.GetService<ILogger<Program>>() ?? 
                     new LoggerFactory().CreateLogger("Fallback");
        var maxAttempts = 5;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                logger.LogInformation("Ensuring database exists (attempt {Attempt}/{Max})", attempt, maxAttempts);
                db.Database.EnsureCreated();
                logger.LogInformation("Database ensured");
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Migration attempt {Attempt} failed", attempt);
                if (attempt == maxAttempts)
                {
                    logger.LogError("All migration attempts failed — database may be unreachable");
                }
                else
                {
                    await Task.Delay(2000 * attempt); // backoff
                }
            }
        }
    });

// Swagger (Development only)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseRateLimiter();
app.UseErrorHandling();
// Disable API key auth in Development for local demo
Console.WriteLine($"[STARTUP] Env: {app.Environment.EnvironmentName}, Auth enabled: {!app.Environment.IsDevelopment()}");
if (!app.Environment.IsDevelopment())
{
    app.UseApiKeyAuthentication();
}

app.MapPaymentsEndpoints();
app.MapWebhookEndpoints();
app.MapSettlementsEndpoints();
app.MapDashboardEndpoints();

// Hangfire dashboard only when configured
if (enableHangfire)
{
    app.MapHangfireDashboard("/admin/hangfire");
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapGet("/health/ready", async (HttpContext context) =>
{
    try
    {
        var db = context.RequestServices.GetRequiredService<PayFlowDbContext>();
        await db.Database.CanConnectAsync();
        return Results.Ok(new { status = "healthy" });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[HEALTH] Unhealthy: {ex}");
        return Results.Json(new { status = "unhealthy" }, statusCode: 503);
    }
});

app.MapGet("/health/live", () => Results.Ok(new { status = "alive" }));

app.Run();

public class TenantContextWrapper : PayFlow.Application.Interfaces.ITenantContext, PayFlow.Infrastructure.MultiTenancy.ITenantContext
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

public partial class Program { }
