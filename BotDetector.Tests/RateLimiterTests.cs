using System;
using System.Threading.Tasks;
using BotDetector.Business.Configurations;
using BotDetector.Business.Interfaces;
using BotDetector.Infrastructure.Implementation;
using BotDetector.Infrastructure.Redis;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace BotDetector.Tests
{
    public class RateLimiterTests
    {
        [Fact]
        public async Task InMemoryRateLimiter_AllowsRequestsWithinLimit_BlocksWhenExceeded()
        {
            // Arrange
            var cache = new MemoryCache(new MemoryCacheOptions());
            var limiter = new InMemoryRateLimiter(cache);
            string key = "test-client";
            int limit = 3;
            var window = TimeSpan.FromSeconds(5);

            // Act & Assert
            // 1st request - Allow
            Assert.True(await limiter.IsAllowedAsync(key, limit, window));
            // 2nd request - Allow
            Assert.True(await limiter.IsAllowedAsync(key, limit, window));
            // 3rd request - Allow
            Assert.True(await limiter.IsAllowedAsync(key, limit, window));
            // 4th request - Block
            Assert.False(await limiter.IsAllowedAsync(key, limit, window));
        }

        [Fact]
        public async Task RedisRateLimiter_WhenDisabled_FallsBackToInMemoryRateLimiter()
        {
            // Arrange
            var mockFallback = new Mock<IRateLimiter>();
            mockFallback.Setup(f => f.IsAllowedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<TimeSpan>()))
                .ReturnsAsync(true);

            var options = Options.Create(new RateLimitOptions
            {
                EnableRedis = false,
                RedisConnectionString = ""
            });

            var mockLogger = new Mock<ILogger<RedisRateLimiter>>();

            var redisRateLimiter = new RedisRateLimiter(mockFallback.Object, options, mockLogger.Object);

            // Act
            var result = await redisRateLimiter.IsAllowedAsync("some-key", 10, TimeSpan.FromMinutes(1));

            // Assert
            Assert.True(result);
            mockFallback.Verify(f => f.IsAllowedAsync("some-key", 10, It.IsAny<TimeSpan>()), Times.Once);
        }

        [Fact]
        public async Task RedisRateLimiter_WhenRedisThrowsException_GracefullyFallsBackToInMemory()
        {
            // Arrange
            var mockFallback = new Mock<IRateLimiter>();
            mockFallback.Setup(f => f.IsAllowedAsync("failing-key", 5, It.IsAny<TimeSpan>()))
                .ReturnsAsync(false); // mock fallback returns false

            // Enable Redis, but point to invalid connection string which will cause exception during connection/evaluation
            var options = Options.Create(new RateLimitOptions
            {
                EnableRedis = true,
                RedisConnectionString = "localhost:9999,abortConnect=true"
            });

            var mockLogger = new Mock<ILogger<RedisRateLimiter>>();

            var redisRateLimiter = new RedisRateLimiter(mockFallback.Object, options, mockLogger.Object);

            // Act
            // Since connection to port 9999 will fail or throw, it must catch it and use fallback
            var result = await redisRateLimiter.IsAllowedAsync("failing-key", 5, TimeSpan.FromMinutes(1));

            // Assert
            Assert.False(result); // Matches the fallback response
            mockFallback.Verify(f => f.IsAllowedAsync("failing-key", 5, It.IsAny<TimeSpan>()), Times.Once);
        }
    }
}
