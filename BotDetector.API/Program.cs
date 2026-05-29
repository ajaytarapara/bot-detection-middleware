using BotDetector.API.Middleware;
using BotDetector.Business.Configurations;
using BotDetector.Business.Interfaces;
using BotDetector.Business.Rules;
using BotDetector.Business.Services;
using BotDetector.Infrastructure.Implementation;
using BotDetector.Infrastructure.Logging;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<DetectionThresholds>(
    builder.Configuration.GetSection("DetectionThresholds"));

builder.Services.Configure<RuleWeightsOptions>(
    builder.Configuration.GetSection("RuleWeights"));

builder.Services.Configure<RateLimitOptions>(
    builder.Configuration.GetSection("RateLimitOptions"));

builder.Services.AddScoped<IDetectionRule, UserAgentRule>();

builder.Services.AddScoped<IDetectionRule, HeaderAnalysisRule>();

builder.Services.AddScoped<IDetectionEngine, DetectionEngine>();

builder.Services.AddMemoryCache();

builder.Services.AddScoped<IRateLimiter, InMemoryRateLimiter>();

builder.Services.AddScoped<IDetectionRule, RateLimitRule>();

builder.Services.AddScoped<ITrafficClassifier, TrafficClassifier>();

builder.Services.AddScoped<IAuditLogger, AuditLogger>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseBotDetection();
app.MapControllers();
app.Run();
