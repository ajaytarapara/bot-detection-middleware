using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BotDetector.Business.Interfaces;
using BotDetector.Common.Enums;

namespace BotDetector.Business.Services
{
    using BotDetector.Business.Configurations;
    using BotDetector.Business.Interfaces;
    using BotDetector.Business.Models;
    using Microsoft.Extensions.Options;
    public class DetectionEngine : IDetectionEngine
    {
        private readonly IEnumerable<IDetectionRule> _rules;
        private readonly DetectionThresholds _thresholds;
        public DetectionEngine(IEnumerable<IDetectionRule> rules, IOptions<DetectionThresholds> thresholds)
        {
            _rules = rules;
            _thresholds = thresholds.Value;
        }

        public async Task<DetectionResult> AnalyzeAsync(
            BotRequestContext context)
        {
            var results = new List<RuleResult>();

            foreach (var rule in _rules)
            {
                var result = await rule.EvaluateAsync(context);
                results.Add(result);
            }
            var totalScore = results.Sum(x => x.Score);

            var action = GetAction(totalScore);

            return new DetectionResult
            {
                TotalScore = totalScore,
                Action = action,
                RuleResults = results,
                Reasons = results.Where(x => x.IsSuspicious).Select(x => x.Reason).ToList()
            };
        }

        private BotAction GetAction(int score)
        {
            if (score <= _thresholds.AllowMax)
                return BotAction.Allow;

            if (score <= _thresholds.ShadowMax)
                return BotAction.Shadow;

            if (score <= _thresholds.ChallengeMax)
                return BotAction.Challenge;

            if (score <= _thresholds.ThrottleMax)
                return BotAction.Throttle;

            return BotAction.Block;
        }
    }
}