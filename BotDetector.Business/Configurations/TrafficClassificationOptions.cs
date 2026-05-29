using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BotDetector.Business.Configurations
{
    public class TrafficClassificationOptions
    {
        public List<string> InternalPaths { get; set; } = new();

        public List<string> TrustedApiKeys { get; set; } = new();

        public List<string> VerifiedBotUserAgents { get; set; } = new();

        public List<string> BlockedIps { get; set; } = new();

        public List<string> TrustedIps { get; set; } = new();

        public List<string> TrustedProxies { get; set; } = new();

        public string ApiKeyHeader { get; set; } = "X-API-Key";
    }
}