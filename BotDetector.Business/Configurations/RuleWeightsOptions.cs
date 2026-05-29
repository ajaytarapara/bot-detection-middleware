using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BotDetector.Business.Configurations
{
    public class RuleWeightsOptions
    {
        public int MissingUserAgentScore { get; set; }

        public int SuspiciousUserAgentScore { get; set; }

        public int MissingAcceptLanguageScore { get; set; }

        public int MissingAcceptEncodingScore { get; set; }

        public int RateLimitExceededScore { get; set; }
    }
}