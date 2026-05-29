using System;
using System.Threading.Tasks;
using BotDetector.Business.Interfaces;
using BotDetector.Business.Configurations;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace BotDetector.Infrastructure.Redis
{
    public class RedisRateLimiter : IRateLimiter
    {
        private readonly IRateLimiter _fallbackLimiter;
        private readonly RateLimitOptions _options;
        private readonly ILogger<RedisRateLimiter> _logger;
        private readonly Lazy<IConnectionMultiplexer?> _lazyConnection;
        private bool _isRedisHealthy = true;

        private const string LuaScript = @"
            local key = KEYS[1]
            local now = tonumber(ARGV[1])
            local window = tonumber(ARGV[2])
            local limit = tonumber(ARGV[3])
            local member = ARGV[4]
            local clearBefore = now - window

            redis.call('ZREMRANGEBYSCORE', key, 0, clearBefore)
            local currentRequests = redis.call('ZCARD', key)

            if currentRequests < limit then
                redis.call('ZADD', key, now, member)
                -- set TTL slightly longer than window to ensure cleanup
                redis.call('PEXPIRE', key, window + 1000)
                return 1
            else
                return 0
            end";

        public RedisRateLimiter(
            IRateLimiter fallbackLimiter,
            IOptions<RateLimitOptions> options,
            ILogger<RedisRateLimiter> logger)
        {
            _fallbackLimiter = fallbackLimiter;
            _options = options.Value;
            _logger = logger;

            _lazyConnection = new Lazy<IConnectionMultiplexer?>(() =>
            {
                if (!_options.EnableRedis || string.IsNullOrWhiteSpace(_options.RedisConnectionString))
                {
                    _logger.LogInformation("Redis rate limiting is disabled or connection string is empty. Using memory fallback.");
                    _isRedisHealthy = false;
                    return null;
                }

                try
                {
                    var connection = ConnectionMultiplexer.Connect(_options.RedisConnectionString);
                    connection.ConnectionFailed += (sender, e) =>
                    {
                        _logger.LogWarning("Redis connection failed: {Message}. Degrading to memory rate limiter.", e.Exception?.Message);
                        _isRedisHealthy = false;
                    };
                    connection.ConnectionRestored += (sender, e) =>
                    {
                        _logger.LogInformation("Redis connection restored. Resuming distributed rate limiting.");
                        _isRedisHealthy = true;
                    };
                    return connection;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to connect to Redis at startup. Falling back to memory rate limiting.");
                    _isRedisHealthy = false;
                    return null;
                }
            });
        }

        public async Task<bool> IsAllowedAsync(string key, int limit, TimeSpan window)
        {
            if (!_options.EnableRedis || !_isRedisHealthy)
            {
                return await _fallbackLimiter.IsAllowedAsync(key, limit, window);
            }

            try
            {
                var connection = _lazyConnection.Value;
                if (connection == null || !connection.IsConnected)
                {
                    _isRedisHealthy = false;
                    _logger.LogWarning("Redis connection is unavailable. Using memory fallback for key: {Key}", key);
                    return await _fallbackLimiter.IsAllowedAsync(key, limit, window);
                }

                var db = connection.GetDatabase();
                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var windowMs = (long)window.TotalMilliseconds;
                var member = $"{now}_{Guid.NewGuid()}";

                var result = await db.ScriptEvaluateAsync(
                    LuaScript,
                    new RedisKey[] { $"ratelimit:{key}" },
                    new RedisValue[] { now, windowMs, limit, member });

                return (int)result == 1;
            }
            catch (Exception ex)
            {
                _isRedisHealthy = false;
                _logger.LogError(ex, "Error executing Redis rate limit check. Falling back to memory for key: {Key}", key);
                return await _fallbackLimiter.IsAllowedAsync(key, limit, window);
            }
        }
    }
}
