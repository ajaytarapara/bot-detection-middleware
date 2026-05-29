using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BotDetector.Business.Configurations
{
    public class RateLimitOptions
    {
        public int RequestsPerMinute { get; set; }
    }
}