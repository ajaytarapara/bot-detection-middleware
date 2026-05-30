using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BotDetector.Business.Configurations
{
    public class RuleWeightsOptions
    {
        public int MissingUserAgentScore { get; set; } = 25;
        public int SuspiciousUserAgentScore { get; set; } = 20;
        public int MissingAcceptLanguageScore { get; set; } = 10;
        public int MissingAcceptEncodingScore { get; set; } = 10;
        public int RateLimitExceededScore { get; set; } = 40;
    }
}