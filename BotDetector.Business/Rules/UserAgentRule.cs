using BotDetector.Business.Configurations;
using BotDetector.Business.Interfaces;
using BotDetector.Business.Models;
using Microsoft.Extensions.Options;

namespace BotDetector.Business.Rules;

public class UserAgentRule : IDetectionRule
{
    private readonly RuleWeightsOptions _weights;

    public UserAgentRule(
        IOptions<RuleWeightsOptions> options)
    {
        _weights = options.Value;
    }

    public Task<RuleResult> EvaluateAsync(
        BotRequestContext context)
    {
        if (string.IsNullOrWhiteSpace(context.UserAgent))
        {
            return Task.FromResult(
                new RuleResult
                {
                    RuleName = nameof(UserAgentRule),
                    Score = _weights.MissingUserAgentScore,
                    IsSuspicious = true,
                    Reason = "Missing User-Agent"
                });
        }

        string userAgent = context.UserAgent.ToLower();

        var suspiciousAgents = new[]
        {
            "curl",
            "wget",
            "python-requests",
            "scrapy",
            "aiohttp"
        };

        if (suspiciousAgents.Any(x => userAgent.Contains(x)))
        {
            return Task.FromResult(
                new RuleResult
                {
                    RuleName = nameof(UserAgentRule),
                    Score = _weights.SuspiciousUserAgentScore,
                    IsSuspicious = true,
                    Reason = "Suspicious User-Agent"
                });
        }

        return Task.FromResult(
            new RuleResult
            {
                RuleName = nameof(UserAgentRule),
                Score = 0,
                IsSuspicious = false,
                Reason = string.Empty
            });
    }
}