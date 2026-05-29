using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BotDetector.Business.Interfaces;
using BotDetector.Business.Models;
using BotDetector.Common.Enums;

namespace BotDetector.API.Middleware
{
    public class BotDetectionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<BotDetectionMiddleware> _logger;

        public BotDetectionMiddleware(
            RequestDelegate next,
            ILogger<BotDetectionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }
        public async Task InvokeAsync(
            HttpContext context,
            ITrafficClassifier trafficClassifier,
            IDetectionEngine detectionEngine,
            IAuditLogger auditLogger)
        {
            var requestContext = BuildRequestContext(context);

            var classification =
                await trafficClassifier.ClassifyAsync(requestContext);

            if (classification.BypassDetection)
            {
                await _next(context);
                return;
            }

            if (classification.TrafficType == TrafficType.KnownAbuser)
            {
                context.Response.StatusCode =
                    StatusCodes.Status403Forbidden;

                await context.Response.WriteAsJsonAsync(new
                {
                    Message = "Blocked - Known Abuser"
                });

                return;
            }

            var detectionResult =
                await detectionEngine.AnalyzeAsync(requestContext);
            Console.WriteLine(
                System.Text.Json.JsonSerializer.Serialize(
                    detectionResult));
            // Audit Log
            await auditLogger.LogAsync(
                new RequestAudit
                {
                    IpAddress = requestContext.IpAddress,
                    Path = requestContext.Path,
                    Method = requestContext.Method,
                    Score = detectionResult.TotalScore,
                    Action = detectionResult.Action.ToString(),
                    Reasons = detectionResult.Reasons,
                    TimestampUtc = DateTime.UtcNow
                });

            _logger.LogInformation(
                "Bot Detection Result: {@DetectionResult}",
                detectionResult);

            var stopPipeline =
                await HandleAction(context, detectionResult);

            if (stopPipeline)
            {
                return;
            }

            await _next(context);
        }
        private BotRequestContext BuildRequestContext(
               HttpContext context)
        {
            return new BotRequestContext
            {
                IpAddress =
                    context.Connection.RemoteIpAddress?.ToString() ?? string.Empty,

                UserAgent =
                    context.Request.Headers["User-Agent"].ToString(),

                Path =
                    context.Request.Path,

                Method =
                    context.Request.Method,

                RequestTimeUtc =
                    DateTime.UtcNow,

                Headers =
                    context.Request.Headers
                        .ToDictionary(
                            h => h.Key,
                            h => h.Value.ToString())
            };
        }
        private async Task<bool> HandleAction(
               HttpContext context,
               DetectionResult result)
        {
            switch (result.Action)
            {
                case BotAction.Allow:
                    return false;

                case BotAction.Shadow:

                    _logger.LogWarning(
                        "Shadow traffic detected. Score: {Score}",
                        result.TotalScore);

                    return false;

                case BotAction.Challenge:

                    context.Response.StatusCode =
                        StatusCodes.Status403Forbidden;

                    await context.Response.WriteAsJsonAsync(new
                    {
                        Challenge = true,
                        Message = "Verification Required"
                    });

                    return true;

                case BotAction.Throttle:
                    context.Response.StatusCode =
                         StatusCodes.Status429TooManyRequests;

                    await context.Response.WriteAsJsonAsync(new
                    {
                        Message = "Too Many Requests"
                    });

                    return true;

                case BotAction.Block:

                    context.Response.StatusCode =
                        StatusCodes.Status403Forbidden;

                    await context.Response.WriteAsJsonAsync(new
                    {
                        Message = "Blocked"
                    });

                    return true;

                default:
                    return false;
            }
        }
    }
}