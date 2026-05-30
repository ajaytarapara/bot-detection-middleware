using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BotDetector.Business.Interfaces;
using BotDetector.Business.Models;
using BotDetector.Business.Configurations;
using BotDetector.Business.Services;
using BotDetector.Common.Enums;
using Microsoft.Extensions.Options;

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
            IAuditLogger auditLogger,
            IOptions<TrafficClassificationOptions> trafficOptions)
        {
            BotRequestContext? requestContext = null;
            try
            {
                var options = trafficOptions.Value;
                requestContext = BuildRequestContext(context, options);

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

                    // Audit log for known abuser
                    await auditLogger.LogAsync(
                        new RequestAudit
                        {
                            IpAddress = MaskIpAddress(requestContext.IpAddress),
                            Path = requestContext.Path,
                            Method = requestContext.Method,
                            Score = 100,
                            Action = BotAction.Block.ToString(),
                            Reasons = new List<string> { "IP blocklist match" },
                            TimestampUtc = DateTime.UtcNow
                        });

                    return;
                }

                var detectionResult =
                    await detectionEngine.AnalyzeAsync(requestContext);

                // Audit Log (PII Sanitized)
                await auditLogger.LogAsync(
                    new RequestAudit
                    {
                        IpAddress = MaskIpAddress(requestContext.IpAddress),
                        Path = requestContext.Path,
                        Method = requestContext.Method,
                        Score = detectionResult.TotalScore,
                        Action = detectionResult.Action.ToString(),
                        Reasons = detectionResult.Reasons,
                        TimestampUtc = DateTime.UtcNow
                    });

                _logger.LogInformation(
                    "Bot Detection Result: Action={Action}, Score={Score}, ClientIp={ClientIp}, Reasons={Reasons}",
                    detectionResult.Action,
                    detectionResult.TotalScore,
                    MaskIpAddress(requestContext.IpAddress),
                    string.Join(", ", detectionResult.Reasons));

                var stopPipeline =
                    await HandleAction(context, detectionResult);

                if (stopPipeline)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                // FAIL SAFE: Log exception but proceed to prevent breaking the API.
                _logger.LogError(ex, "BotDetectionMiddleware error. Continuing request pipeline safely.");
            }

            await _next(context);
        }

        private BotRequestContext BuildRequestContext(
               HttpContext context,
               TrafficClassificationOptions options)
        {
            var clientIp = GetClientIp(context, options);

            return new BotRequestContext
            {
                IpAddress = clientIp,

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

        private string GetClientIp(HttpContext context, TrafficClassificationOptions options)
        {
            var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? string.Empty;

            // Strip port if present in remoteIp
            int colonIndex = remoteIp.LastIndexOf(':');
            if (colonIndex > 0 && remoteIp.Count(c => c == ':') == 1)
            {
                remoteIp = remoteIp.Substring(0, colonIndex);
            }

            if (!context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor) || string.IsNullOrWhiteSpace(forwardedFor))
            {
                return remoteIp;
            }

            var ips = forwardedFor.ToString()
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(ip => ip.Trim())
                .ToList();

            if (!ips.Any())
            {
                return remoteIp;
            }

            // Check if the immediate connection proxy is trusted
            if (options.TrustedProxies.Any(proxy => TrafficClassifier.IsIpInCidr(remoteIp, proxy)))
            {
                // Traverse proxy chain from right to left
                for (int i = ips.Count - 1; i >= 0; i--)
                {
                    var currentIp = ips[i];
                    if (i == 0)
                    {
                        return currentIp; // Client IP
                    }

                    if (!options.TrustedProxies.Any(proxy => TrafficClassifier.IsIpInCidr(currentIp, proxy)))
                    {
                        return currentIp; // Client IP
                    }
                }
            }

            return remoteIp;
        }

        private string MaskIpAddress(string ipAddress)
        {
            if (string.IsNullOrEmpty(ipAddress)) return string.Empty;

            if (ipAddress.Contains('.'))
            {
                var parts = ipAddress.Split('.');
                if (parts.Length >= 4)
                {
                    return $"{parts[0]}.{parts[1]}.xxx.xxx";
                }
            }

            if (ipAddress.Contains(':'))
            {
                var parts = ipAddress.Split(':');
                if (parts.Length >= 2)
                {
                    return $"{parts[0]}:{parts[1]}:xxxx:xxxx:xxxx:xxxx:xxxx:xxxx";
                }
            }

            return "xxx.xxx.xxx.xxx";
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

                case BotAction.Tarpit:
                    _logger.LogWarning("Tarpitting connection. Applying artificial delay.");
                    await Task.Delay(5000); // 5 seconds delay to waste bot resources
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