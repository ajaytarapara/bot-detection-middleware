using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BotDetector.Business.Configurations
{
    public class RateLimitOptions
    {
        public int RequestsPerMinute { get; set; }
        public string RedisConnectionString { get; set; } = string.Empty;
        public bool EnableRedis { get; set; }
        public Dictionary<string, int> EndpointLimits { get; set; } = new();
        public int DefaultLimit { get; set; } = 60;
    }
}