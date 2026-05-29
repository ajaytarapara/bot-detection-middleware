using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BotDetector.Business.Configurations;
using BotDetector.Business.Models;
using BotDetector.Business.Services;
using BotDetector.Common.Enums;
using Microsoft.Extensions.Options;
using Xunit;

namespace BotDetector.Tests
{
    public class TrafficClassifierTests
    {
        private readonly TrafficClassificationOptions _options;

        public TrafficClassifierTests()
        {
            _options = new TrafficClassificationOptions
            {
                InternalPaths = new List<string> { "/health", "/metrics", "/webhooks/*" },
                TrustedApiKeys = new List<string> { "key-123", "partner-abc" },
                VerifiedBotUserAgents = new List<string> { "Googlebot", "Bingbot" },
                BlockedIps = new List<string> { "1.1.1.1", "10.0.0.0/24" },
                TrustedIps = new List<string> { "192.168.1.50", "172.16.0.0/12" },
                ApiKeyHeader = "X-API-Key"
            };
        }

        [Fact]
        public async Task ClassifyAsync_BypassesInternalPaths()
        {
            // Arrange
            var classifier = new TrafficClassifier(Options.Create(_options));
            
            var context1 = new BotRequestContext { Path = "/health", IpAddress = "12.34.56.78", Headers = new() };
            var context2 = new BotRequestContext { Path = "/webhooks/receive-payment", IpAddress = "12.34.56.78", Headers = new() };
            var context3 = new BotRequestContext { Path = "/api/products", IpAddress = "12.34.56.78", Headers = new() };

            // Act
            var res1 = await classifier.ClassifyAsync(context1);
            var res2 = await classifier.ClassifyAsync(context2);
            var res3 = await classifier.ClassifyAsync(context3);

            // Assert
            Assert.True(res1.BypassDetection);
            Assert.Equal(TrafficType.InternalService, res1.TrafficType);

            Assert.True(res2.BypassDetection);
            Assert.Equal(TrafficType.InternalService, res2.TrafficType);

            Assert.False(res3.BypassDetection);
            Assert.Equal(TrafficType.Unknown, res3.TrafficType);
        }

        [Fact]
        public async Task ClassifyAsync_BlocksBlockedIps()
        {
            // Arrange
            var classifier = new TrafficClassifier(Options.Create(_options));
            
            var context1 = new BotRequestContext { Path = "/api/products", IpAddress = "1.1.1.1", Headers = new() };
            var context2 = new BotRequestContext { Path = "/api/products", IpAddress = "10.0.0.50", Headers = new() };
            var context3 = new BotRequestContext { Path = "/api/products", IpAddress = "10.0.1.50", Headers = new() };

            // Act
            var res1 = await classifier.ClassifyAsync(context1);
            var res2 = await classifier.ClassifyAsync(context2);
            var res3 = await classifier.ClassifyAsync(context3);

            // Assert
            Assert.Equal(TrafficType.KnownAbuser, res1.TrafficType);
            Assert.False(res1.BypassDetection);

            Assert.Equal(TrafficType.KnownAbuser, res2.TrafficType);
            Assert.False(res2.BypassDetection);

            Assert.Equal(TrafficType.Unknown, res3.TrafficType);
        }

        [Fact]
        public async Task ClassifyAsync_AllowsTrustedIps()
        {
            // Arrange
            var classifier = new TrafficClassifier(Options.Create(_options));
            
            var context1 = new BotRequestContext { Path = "/api/products", IpAddress = "192.168.1.50", Headers = new() };
            var context2 = new BotRequestContext { Path = "/api/products", IpAddress = "172.20.10.15", Headers = new() };

            // Act
            var res1 = await classifier.ClassifyAsync(context1);
            var res2 = await classifier.ClassifyAsync(context2);

            // Assert
            Assert.Equal(TrafficType.Human, res1.TrafficType);
            Assert.True(res1.BypassDetection);

            Assert.Equal(TrafficType.Human, res2.TrafficType);
            Assert.True(res2.BypassDetection);
        }

        [Fact]
        public async Task ClassifyAsync_AllowsApiPartners()
        {
            // Arrange
            var classifier = new TrafficClassifier(Options.Create(_options));
            
            var context = new BotRequestContext 
            { 
                Path = "/api/products", 
                IpAddress = "12.34.56.78", 
                Headers = new Dictionary<string, string> { { "X-API-Key", "partner-abc" } } 
            };

            // Act
            var result = await classifier.ClassifyAsync(context);

            // Assert
            Assert.Equal(TrafficType.ApiPartner, result.TrafficType);
            Assert.True(result.BypassDetection);
        }

        [Fact]
        public async Task ClassifyAsync_SpoofedBotUserAgent_ReturnsUnknownTrafficAndRunsDetection()
        {
            // Arrange
            var classifier = new TrafficClassifier(Options.Create(_options));
            
            var context = new BotRequestContext 
            { 
                Path = "/api/products", 
                IpAddress = "1.2.3.4", // Normal IP - DNS will fail verification
                UserAgent = "Mozilla/5.0 Googlebot/2.1",
                Headers = new() 
            };

            // Act
            var result = await classifier.ClassifyAsync(context);

            // Assert
            // Since DNS lookup on 1.2.3.4 will not reverse resolve to googlebot.com/google.com, it is classified as Unknown/Anonymous and subject to rules
            Assert.Equal(TrafficType.Unknown, result.TrafficType);
            Assert.False(result.BypassDetection);
            Assert.Contains("Spoofed crawler", result.Reason);
        }
    }
}
