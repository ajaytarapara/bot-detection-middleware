using BotDetector.Business.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace BotDetector.Infrastructure.Implementation;

public class InMemoryRateLimiter : IRateLimiter
{
    private readonly IMemoryCache _cache;

    public InMemoryRateLimiter(
        IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task<bool> IsAllowedAsync(
        string key,
        int limit,
        TimeSpan window)
    {
        int currentCount =
            _cache.Get<int>(key);

        currentCount++;

        _cache.Set(
            key,
            currentCount,
            window);

        return Task.FromResult(
            currentCount <= limit);
    }
}