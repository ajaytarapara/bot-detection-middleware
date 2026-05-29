using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BotDetector.Business.Interfaces
{
    public interface IRateLimiter
    {
        Task<bool> IsAllowedAsync(string key, int limit, TimeSpan window);
    }

}