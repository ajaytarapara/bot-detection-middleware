using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using BotDetector.API.Middleware;
using BotDetector.Business.Configurations;
using BotDetector.Business.Interfaces;
using BotDetector.Business.Models;
using BotDetector.Common.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace BotDetector.Tests
{
    public class BotDetectionMiddlewareTests
    {
        private readonly Mock<ITrafficClassifier> _mockClassifier;
        private readonly Mock<IDetectionEngine> _mockEngine;
        private readonly Mock<IAuditLogger> _mockAuditLogger;
        private readonly Mock<ILogger<BotDetectionMiddleware>> _mockLogger;
        private readonly IOptions<TrafficClassificationOptions> _trafficOptions;

        public BotDetectionMiddlewareTests()
        {
            _mockClassifier = new Mock<ITrafficClassifier>();
            _mockEngine = new Mock<IDetectionEngine>();
            _mockAuditLogger = new Mock<IAuditLogger>();
            _mockLogger = new Mock<ILogger<BotDetectionMiddleware>>();
            _trafficOptions = Options.Create(new TrafficClassificationOptions
            {
                TrustedProxies = new List<string> { "10.0.0.1" }
            });
        }

        [Fact]
        public async Task InvokeAsync_AllowAction_CallsNextDelegate()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
            httpContext.Request.Path = "/api/products";
            httpContext.Request.Method = "GET";

            bool nextCalled = false;
            RequestDelegate next = (ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            };

            _mockClassifier.Setup(c => c.ClassifyAsync(It.IsAny<BotRequestContext>()))
                .ReturnsAsync(new TrafficClassificationResult { TrafficType = TrafficType.Human, BypassDetection = false });

            _mockEngine.Setup(e => e.AnalyzeAsync(It.IsAny<BotRequestContext>()))
                .ReturnsAsync(new DetectionResult { Action = BotAction.Allow, TotalScore = 0, Reasons = new() });

            var middleware = new BotDetectionMiddleware(next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(httpContext, _mockClassifier.Object, _mockEngine.Object, _mockAuditLogger.Object, _trafficOptions);

            // Assert
            Assert.True(nextCalled);
            Assert.Equal(200, httpContext.Response.StatusCode);
        }

        [Fact]
        public async Task InvokeAsync_BlockAction_Returns403AndStopsPipeline()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
            httpContext.Response.Body = new MemoryStream();

            bool nextCalled = false;
            RequestDelegate next = (ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            };

            _mockClassifier.Setup(c => c.ClassifyAsync(It.IsAny<BotRequestContext>()))
                .ReturnsAsync(new TrafficClassificationResult { TrafficType = TrafficType.Unknown, BypassDetection = false });

            _mockEngine.Setup(e => e.AnalyzeAsync(It.IsAny<BotRequestContext>()))
                .ReturnsAsync(new DetectionResult { Action = BotAction.Block, TotalScore = 95, Reasons = new List<string> { "Suspicious UA" } });

            var middleware = new BotDetectionMiddleware(next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(httpContext, _mockClassifier.Object, _mockEngine.Object, _mockAuditLogger.Object, _trafficOptions);

            // Assert
            Assert.False(nextCalled);
            Assert.Equal(403, httpContext.Response.StatusCode);
            
            httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(httpContext.Response.Body);
            var responseText = await reader.ReadToEndAsync();
            Assert.Contains("Blocked", responseText);
        }

        [Fact]
        public async Task InvokeAsync_TarpitAction_AppliesDelayAndReturns403()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.1");
            httpContext.Response.Body = new MemoryStream();

            bool nextCalled = false;
            RequestDelegate next = (ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            };

            _mockClassifier.Setup(c => c.ClassifyAsync(It.IsAny<BotRequestContext>()))
                .ReturnsAsync(new TrafficClassificationResult { TrafficType = TrafficType.Unknown, BypassDetection = false });

            _mockEngine.Setup(e => e.AnalyzeAsync(It.IsAny<BotRequestContext>()))
                .ReturnsAsync(new DetectionResult { Action = BotAction.Tarpit, TotalScore = 100, Reasons = new List<string> { "Known scraper patterns" } });

            var middleware = new BotDetectionMiddleware(next, _mockLogger.Object);

            // Act
            var startTime = DateTime.UtcNow;
            await middleware.InvokeAsync(httpContext, _mockClassifier.Object, _mockEngine.Object, _mockAuditLogger.Object, _trafficOptions);
            var duration = DateTime.UtcNow - startTime;

            // Assert
            Assert.False(nextCalled);
            Assert.Equal(403, httpContext.Response.StatusCode);
            Assert.True(duration.TotalMilliseconds >= 4500, $"Tarpit did not delay long enough: {duration.TotalMilliseconds}ms");
        }

        [Fact]
        public async Task InvokeAsync_LogsSanitizedIpAddress_MasksPii()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.123");
            httpContext.Request.Path = "/api/products";
            httpContext.Request.Method = "GET";

            _mockClassifier.Setup(c => c.ClassifyAsync(It.IsAny<BotRequestContext>()))
                .ReturnsAsync(new TrafficClassificationResult { TrafficType = TrafficType.Human, BypassDetection = false });

            _mockEngine.Setup(e => e.AnalyzeAsync(It.IsAny<BotRequestContext>()))
                .ReturnsAsync(new DetectionResult { Action = BotAction.Allow, TotalScore = 0, Reasons = new() });

            RequestAudit capturedAudit = null!;
            _mockAuditLogger.Setup(a => a.LogAsync(It.IsAny<RequestAudit>()))
                .Callback<RequestAudit>(a => capturedAudit = a)
                .Returns(Task.CompletedTask);

            var middleware = new BotDetectionMiddleware((ctx) => Task.CompletedTask, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(httpContext, _mockClassifier.Object, _mockEngine.Object, _mockAuditLogger.Object, _trafficOptions);

            // Assert
            Assert.NotNull(capturedAudit);
            Assert.Equal("192.168.xxx.xxx", capturedAudit.IpAddress); // Verify IPv4 masking
        }

        [Fact]
        public async Task InvokeAsync_ProxyChainValidation_ExtractsCorrectClientIp()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            // Connection comes from trusted proxy 10.0.0.1
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");
            // X-Forwarded-For contains: client, intermediate-proxy
            httpContext.Request.Headers["X-Forwarded-For"] = "192.168.1.25, 10.0.0.1";
            httpContext.Request.Path = "/api/products";

            BotRequestContext capturedContext = null!;
            _mockClassifier.Setup(c => c.ClassifyAsync(It.IsAny<BotRequestContext>()))
                .Callback<BotRequestContext>(c => capturedContext = c)
                .ReturnsAsync(new TrafficClassificationResult { TrafficType = TrafficType.Human, BypassDetection = false });

            _mockEngine.Setup(e => e.AnalyzeAsync(It.IsAny<BotRequestContext>()))
                .ReturnsAsync(new DetectionResult { Action = BotAction.Allow, TotalScore = 0, Reasons = new() });

            var middleware = new BotDetectionMiddleware((ctx) => Task.CompletedTask, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(httpContext, _mockClassifier.Object, _mockEngine.Object, _mockAuditLogger.Object, _trafficOptions);

            // Assert
            Assert.NotNull(capturedContext);
            // Since 10.0.0.1 is trusted proxy, we traverse and get 192.168.1.25 as client IP
            Assert.Equal("192.168.1.25", capturedContext.IpAddress);
        }
    }
}
