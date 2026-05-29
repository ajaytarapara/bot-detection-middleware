using BotDetector.Business.Configurations;
using BotDetector.Business.Interfaces;
using BotDetector.Business.Models;
using Microsoft.Extensions.Options;

namespace BotDetector.Business.Rules;

public class HeaderAnalysisRule : IDetectionRule
{
    private readonly RuleWeightsOptions _weights;

    public HeaderAnalysisRule(
        IOptions<RuleWeightsOptions> options)
    {
        _weights = options.Value;
    }

    public Task<RuleResult> EvaluateAsync(
        BotRequestContext context)
    {
        int score = 0;
        string reason = string.Empty;

        if (!context.Headers.ContainsKey("Accept-Language"))
        {
            score += _weights.MissingAcceptLanguageScore;
            reason += "Missing Accept-Language. ";
        }

        if (!context.Headers.ContainsKey("Accept-Encoding"))
        {
            score += _weights.MissingAcceptEncodingScore;
            reason += "Missing Accept-Encoding.";
        }

        return Task.FromResult(
            new RuleResult
            {
                RuleName = nameof(HeaderAnalysisRule),
                Score = score,
                IsSuspicious = score > 0,
                Reason = reason.Trim()
            });
    }
}