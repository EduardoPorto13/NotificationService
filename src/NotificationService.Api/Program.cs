using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;
using Polly.CircuitBreaker;
using NotificationService.Domain.Settings;
using NotificationService.Application.Interfaces;
using NotificationService.Application.Services;
using NotificationService.Domain.Interfaces;
using NotificationService.Infrastructure.Email.Providers;
using NotificationService.Infrastructure.Email.Templates;
using NotificationService.Infrastructure.Logging;
using NotificationService.Infrastructure.Messaging.Providers;
using NotificationService.Infrastructure.Messaging.RabbitMq;
using NotificationService.Infrastructure.Persistence.Context;
using NotificationService.Infrastructure.Persistence.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Configuração opcional por seção
builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.Configure<EmailProviderSettings>(builder.Configuration.GetSection("EmailProvider"));
builder.Services.Configure<LogConfig>(builder.Configuration.GetSection("LogConfig"));
builder.Services.Configure<LogSettings>(builder.Configuration.GetSection("LogSettings"));


// Controllers
builder.Services.AddControllers();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Health Checks
builder.Services.AddHealthChecks()
    .AddSqlServer(
        connectionString: builder.Configuration.GetConnectionString("DefaultConnection")!,
        name: "sqlserver");

// DbContext
builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// === Resilience Policies ===
builder.Services.AddResiliencePipeline("email-provider", pipelineBuilder =>
{
    pipelineBuilder.AddRetry(new RetryStrategyOptions
    {
        ShouldHandle = new PredicateBuilder().Handle<Exception>(),
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromSeconds(2),
        BackoffType = DelayBackoffType.Exponential
    });

    pipelineBuilder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
    {
        ShouldHandle = new PredicateBuilder().Handle<Exception>(),
        FailureRatio = 0.5,
        SamplingDuration = TimeSpan.FromSeconds(10),
        MinimumThroughput = 5,
        BreakDuration = TimeSpan.FromSeconds(30)
    });
});

// === Domain / Infrastructure ===
// Logger Global Singleton para todas as execuções de thread
builder.Services.AddSingleton<IAppLogger, AppLogger>();
builder.Services.AddSingleton<IMessageQueueClient, RabbitMqQueueClient>();

builder.Services.AddScoped<INotificationLogRepository, NotificationLogRepository>();
builder.Services.AddScoped<INotificationProvider, SendGridEmailProvider>();
builder.Services.AddScoped<INotificationProvider, TwilioSmsProvider>();
builder.Services.AddScoped<INotificationProvider, TwilioWhatsAppProvider>();
builder.Services.AddScoped<ITemplateRenderer, FluidTemplateRenderer>();

// === Application ===
builder.Services.AddScoped<INotificationService, NotificationAppService>();
builder.Services.AddScoped<IMessageProcessor, MessageProcessor>();

// === Hosted Services ===
// RabbitMQ Consumer como background service
builder.Services.AddHostedService<NotificationMessageConsumer>();

var app = builder.Build();

// Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
