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
        var allowed =
            await _rateLimiter.IsAllowedAsync(
                context.IpAddress,
                _rateLimitOptions.RequestsPerMinute,
                TimeSpan.FromMinutes(1));

        if (!allowed)
        {
            return new RuleResult
            {
                RuleName = nameof(RateLimitRule),
                Score = _weights.RateLimitExceededScore,
                IsSuspicious = true,
                Reason = "Rate limit exceeded"
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
}