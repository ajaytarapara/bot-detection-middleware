using BotDetector.Business.Configurations;
using BotDetector.Business.Interfaces;
using BotDetector.Business.Models;
using Microsoft.Extensions.Options;

namespace BotDetector.Business.Rules;

public class RateLimitRule : IDetectionRule
{
    private readonly IRateLimiter _rateLimiter;
    private readonly RateLimitOptions _rateLimitOptions;
    private readonly RuleWeightsOptions _weights;

    public RateLimitRule(
        IRateLimiter rateLimiter,
        IOptions<RateLimitOptions> rateLimitOptions,
        IOptions<RuleWeightsOptions> weights)
    {
        _rateLimiter = rateLimiter;
        _rateLimitOptions = rateLimitOptions.Value;
        _weights = weights.Value;
    }

    public async Task<RuleResult> EvaluateAsync(
        BotRequestContext context)
    {
        var path = context.Path;
        var limit = GetLimitForPath(path);
        
        // 1. API Key Rate Limiting (if authenticated API partner)
        context.Headers.TryGetValue("X-API-Key", out var apiKey);
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            var apiLimit = limit * 5; // Partners get 5x higher limit
            var apiKeyAllowed = await _rateLimiter.IsAllowedAsync(
                $"rate:apikey:{apiKey}:{path}",
                apiLimit,
                TimeSpan.FromMinutes(1));

            if (!apiKeyAllowed)
            {
                return new RuleResult
                {
                    RuleName = nameof(RateLimitRule),
                    Score = _weights.RateLimitExceededScore,
                    IsSuspicious = true,
                    Reason = $"API key rate limit exceeded for path {path} (limit: {apiLimit}/min)"
                };
            }

            return new RuleResult
            {
                RuleName = nameof(RateLimitRule),
                Score = 0,
                IsSuspicious = false,
                Reason = string.Empty
            };
        }

        // 2. Multi-dimensional Rate Limiting for Anonymous users
        var fingerprint = ComputeFingerprint(context);
        
        // Check 2a: IP + Fingerprint + Path (individual browser/device tracking)
        var clientAllowed = await _rateLimiter.IsAllowedAsync(
            $"rate:client:{context.IpAddress}:{fingerprint}:{path}",
            limit,
            TimeSpan.FromMinutes(1));

        if (!clientAllowed)
        {
            return new RuleResult
            {
                RuleName = nameof(RateLimitRule),
                Score = _weights.RateLimitExceededScore,
                IsSuspicious = true,
                Reason = $"Rate limit exceeded for client fingerprint on path {path} (limit: {limit}/min)"
            };
        }

        // Check 2b: Global IP limit (to protect against distributed/IP-wide scraping, but high enough to allow NAT)
        var globalIpLimit = limit * 10; // 10x standard limit for the entire IP (e.g. 500 employees NAT)
        var globalIpAllowed = await _rateLimiter.IsAllowedAsync(
            $"rate:ip:{context.IpAddress}:{path}",
            globalIpLimit,
            TimeSpan.FromMinutes(1));

        if (!globalIpAllowed)
        {
            return new RuleResult
            {
                RuleName = nameof(RateLimitRule),
                Score = _weights.RateLimitExceededScore,
                IsSuspicious = true,
                Reason = $"Global IP rate limit exceeded for IP {context.IpAddress} on path {path} (limit: {globalIpLimit}/min)"
            };
        }

        return new RuleResult
        {
            RuleName = nameof(RateLimitRule),
            Score = 0,
            IsSuspicious = false,
            Reason = string.Empty
        };
    }

    private int GetLimitForPath(string path)
    {
        if (_rateLimitOptions.EndpointLimits != null)
        {
            if (_rateLimitOptions.EndpointLimits.TryGetValue(path, out var limit))
            {
                return limit;
            }

            foreach (var pair in _rateLimitOptions.EndpointLimits)
            {
                if (pair.Key.EndsWith("*"))
                {
                    var prefix = pair.Key.Substring(0, pair.Key.Length - 1);
                    if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        return pair.Value;
                    }
                }
            }
        }

        return _rateLimitOptions.RequestsPerMinute > 0 
            ? _rateLimitOptions.RequestsPerMinute 
            : _rateLimitOptions.DefaultLimit;
    }

    private string ComputeFingerprint(BotRequestContext context)
    {
        var ua = context.UserAgent ?? string.Empty;
        context.Headers.TryGetValue("Accept-Language", out var lang);
        context.Headers.TryGetValue("Accept-Encoding", out var enc);

        var raw = $"{ua}|{lang ?? string.Empty}|{enc ?? string.Empty}";
        
        // Simple hash logic
        using (var sha = System.Security.Cryptography.SHA256.Create())
        {
            var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(bytes);
        }
    }
}