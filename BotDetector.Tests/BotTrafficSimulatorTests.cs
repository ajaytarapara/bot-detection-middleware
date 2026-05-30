using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace BotDetector.Tests
{
    public class BotTrafficSimulatorTests
    {
        private readonly ITestOutputHelper _output;
        private static readonly HttpClient _client = new HttpClient();
        private const string BaseUrl = "http://localhost:5250";

        private static readonly string[] BrowserUserAgents = new[]
        {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Safari/605.1.15",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:109.0) Gecko/20100101 Firefox/120.0"
        };

        private static readonly string[] BotUserAgents = new[]
        {
            "python-requests/2.31.0",
            "curl/8.4.0",
            "Scrapy/2.11.0"
        };

        public BotTrafficSimulatorTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task RunTrafficSimulation()
        {
            _output.WriteLine("==================================================================");
            _output.WriteLine("                 ANTIGRAVITY BOT TRAFFIC SIMULATOR                ");
            _output.WriteLine("==================================================================");

            // Check if backend API is running
            try
            {
                var response = await _client.GetAsync($"{BaseUrl}/health");
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception("API health check failed.");
                }
                _output.WriteLine("✓ Target API detected at: " + BaseUrl);
            }
            catch
            {
                _output.WriteLine("❌ Target API not running at: " + BaseUrl);
                _output.WriteLine("Please start the API project using: dotnet run --project BotDetector.API");
                _output.WriteLine("Skipping load test simulation because API is unreachable.");
                return;
            }

            _output.WriteLine("\nStarting Attack Scenario Simulations...\n");

            // Execute scenarios
            var naiveResult = await RunNaiveBotScenario();
            var scraperResult = await RunBasicScraperScenario();
            var rotatingResult = await RunRotatingUaScenario();
            var distributedResult = await RunDistributedAttackScenario();
            var slowLowResult = await RunSlowLowScenario();
            var stuffingResult = await RunCredentialStuffingScenario();
            var legitResult = await RunLegitimateTrafficScenario();

            // Print report
            PrintScenarioReport("1. Naive Bot Attack", naiveResult, isBot: true);
            PrintScenarioReport("2. Basic Scraper", scraperResult, isBot: true);
            PrintScenarioReport("3. Rotating User-Agent", rotatingResult, isBot: true);
            PrintScenarioReport("4. Distributed Attack (X-Forwarded-For)", distributedResult, isBot: true);
            PrintScenarioReport("5. Slow and Low (Human-like)", slowLowResult, isBot: true);
            PrintScenarioReport("6. Credential Stuffing (Login POST)", stuffingResult, isBot: true);
            PrintScenarioReport("7. Legitimate Traffic (Real User Mix)", legitResult, isBot: false);

            _output.WriteLine("\n==================================================================");
            _output.WriteLine("                     SIMULATION COMPLETE                          ");
            _output.WriteLine("==================================================================");
        }

        private void PrintScenarioReport(string name, ScenarioResult result, bool isBot)
        {
            _output.WriteLine($"\n--- {name} ---");
            _output.WriteLine($"  Total Requests: {result.TotalRequests}");
            _output.WriteLine($"  Average RPS:    {result.Rps:F1}");
            _output.WriteLine($"  Latency Impact: p50: {result.P50:F1}ms | p95: {result.P95:F1}ms | p99: {result.P99:F1}ms");
            
            // Action Breakdown
            var breakdownParts = result.ActionCounts
                .Where(p => p.Value > 0)
                .Select(p => $"{p.Key}={p.Value}");
            _output.WriteLine($"  Action Breakdown: {string.Join(" ", breakdownParts)}");

            if (isBot)
            {
                double detectionRate = ((double)(result.BlockedCount + result.ThrottledCount + result.ChallengedCount + result.TarpittedCount) / result.TotalRequests) * 100;
                _output.WriteLine($"  Detection Rate (Mitigation Action Taken): {detectionRate:F1}%");
            }
            else
            {
                double falsePositiveRate = ((double)(result.BlockedCount + result.ThrottledCount + result.ChallengedCount + result.TarpittedCount) / result.TotalRequests) * 100;
                _output.WriteLine($"  False Positive Rate (Incorrectly Blocked): {falsePositiveRate:F1}%");
            }
        }

        private async Task<ScenarioResult> RunNaiveBotScenario()
        {
            var latencies = new List<long>();
            var actionCounts = InitializeActionCounts();
            int total = 30; // Reduced request count for test suite speed

            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < total; i++)
            {
                var req = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/api/products");
                req.Headers.Clear();
                
                var reqStopwatch = Stopwatch.StartNew();
                try
                {
                    using var res = await _client.SendAsync(req);
                    reqStopwatch.Stop();
                    long duration = reqStopwatch.ElapsedMilliseconds;
                    latencies.Add(duration);

                    string action = ParseAction(res, duration);
                    actionCounts[action]++;
                }
                catch
                {
                    actionCounts["Block"]++;
                }
            }
            stopwatch.Stop();

            return CalculateResult(total, stopwatch.Elapsed.TotalSeconds, latencies, actionCounts);
        }

        private async Task<ScenarioResult> RunBasicScraperScenario()
        {
            var latencies = new List<long>();
            var actionCounts = InitializeActionCounts();
            int total = 20;

            var stopwatch = Stopwatch.StartNew();
            for (int i = 1; i <= total; i++)
            {
                var req = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/api/products/{i}");
                req.Headers.Add("User-Agent", "python-requests/2.31.0");
                req.Headers.Add("Accept-Language", "en-US");
                req.Headers.Add("Accept-Encoding", "gzip");
                
                var reqStopwatch = Stopwatch.StartNew();
                try
                {
                    using var res = await _client.SendAsync(req);
                    reqStopwatch.Stop();
                    long duration = reqStopwatch.ElapsedMilliseconds;
                    latencies.Add(duration);

                    string action = ParseAction(res, duration);
                    actionCounts[action]++;
                }
                catch
                {
                    actionCounts["Block"]++;
                }

                await Task.Delay(5);
            }
            stopwatch.Stop();

            return CalculateResult(total, stopwatch.Elapsed.TotalSeconds, latencies, actionCounts);
        }

        private async Task<ScenarioResult> RunRotatingUaScenario()
        {
            var latencies = new List<long>();
            var actionCounts = InitializeActionCounts();
            int total = 25;

            var random = new Random();
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < total; i++)
            {
                var req = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/api/products");
                string ua = i % 2 == 0 ? BrowserUserAgents[random.Next(BrowserUserAgents.Length)] : BotUserAgents[random.Next(BotUserAgents.Length)];
                req.Headers.Add("User-Agent", ua);
                req.Headers.Add("Accept-Language", "en-US");
                req.Headers.Add("Accept-Encoding", "gzip");
                
                var reqStopwatch = Stopwatch.StartNew();
                try
                {
                    using var res = await _client.SendAsync(req);
                    reqStopwatch.Stop();
                    long duration = reqStopwatch.ElapsedMilliseconds;
                    latencies.Add(duration);

                    string action = ParseAction(res, duration);
                    actionCounts[action]++;
                }
                catch
                {
                    actionCounts["Block"]++;
                }
            }
            stopwatch.Stop();

            return CalculateResult(total, stopwatch.Elapsed.TotalSeconds, latencies, actionCounts);
        }

        private async Task<ScenarioResult> RunDistributedAttackScenario()
        {
            var latencies = new List<long>();
            var actionCounts = InitializeActionCounts();
            int total = 30;

            var random = new Random();
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < total; i++)
            {
                var req = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/api/search?q=elastic");
                string clientIp = $"{random.Next(1, 255)}.{random.Next(1, 255)}.{random.Next(1, 255)}.{random.Next(1, 255)}";
                req.Headers.Add("X-Forwarded-For", clientIp);
                req.Headers.Add("User-Agent", BrowserUserAgents[random.Next(BrowserUserAgents.Length)]);
                req.Headers.Add("Accept-Language", "en-US");
                req.Headers.Add("Accept-Encoding", "gzip");
                
                var reqStopwatch = Stopwatch.StartNew();
                try
                {
                    using var res = await _client.SendAsync(req);
                    reqStopwatch.Stop();
                    long duration = reqStopwatch.ElapsedMilliseconds;
                    latencies.Add(duration);

                    string action = ParseAction(res, duration);
                    actionCounts[action]++;
                }
                catch
                {
                    actionCounts["Block"]++;
                }
            }
            stopwatch.Stop();

            return CalculateResult(total, stopwatch.Elapsed.TotalSeconds, latencies, actionCounts);
        }

        private async Task<ScenarioResult> RunSlowLowScenario()
        {
            var latencies = new List<long>();
            var actionCounts = InitializeActionCounts();
            int total = 10;

            var random = new Random();
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < total; i++)
            {
                var req = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/api/prices/{i + 1}");
                req.Headers.Add("User-Agent", BrowserUserAgents[random.Next(BrowserUserAgents.Length)]);
                req.Headers.Add("Accept-Language", "en-US,en;q=0.9");
                req.Headers.Add("Accept-Encoding", "gzip, deflate");
                
                var reqStopwatch = Stopwatch.StartNew();
                try
                {
                    using var res = await _client.SendAsync(req);
                    reqStopwatch.Stop();
                    long duration = reqStopwatch.ElapsedMilliseconds;
                    latencies.Add(duration);

                    string action = ParseAction(res, duration);
                    actionCounts[action]++;
                }
                catch
                {
                    actionCounts["Block"]++;
                }

                await Task.Delay(20);
            }
            stopwatch.Stop();

            return CalculateResult(total, stopwatch.Elapsed.TotalSeconds, latencies, actionCounts);
        }

        private async Task<ScenarioResult> RunCredentialStuffingScenario()
        {
            var latencies = new List<long>();
            var actionCounts = InitializeActionCounts();
            // stuffing triggers tarpit, so we keep total count very low for the test suite to execute fast
            int total = 3;

            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < total; i++)
            {
                var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/login");
                req.Content = JsonContent.Create(new { Username = $"bot_{i}", Password = "wrongpassword" });
                req.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120.0.0.0");
                req.Headers.Add("Accept-Language", "en-US");
                req.Headers.Add("Accept-Encoding", "gzip");
                
                var reqStopwatch = Stopwatch.StartNew();
                try
                {
                    using var res = await _client.SendAsync(req);
                    reqStopwatch.Stop();
                    long duration = reqStopwatch.ElapsedMilliseconds;
                    latencies.Add(duration);

                    string action = ParseAction(res, duration);
                    actionCounts[action]++;
                }
                catch
                {
                    actionCounts["Block"]++;
                }
            }
            stopwatch.Stop();

            return CalculateResult(total, stopwatch.Elapsed.TotalSeconds, latencies, actionCounts);
        }

        private async Task<ScenarioResult> RunLegitimateTrafficScenario()
        {
            var latencies = new List<long>();
            var actionCounts = InitializeActionCounts();
            int total = 15;

            var random = new Random();
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < total; i++)
            {
                var req = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/health");
                req.Headers.Add("User-Agent", BrowserUserAgents[random.Next(BrowserUserAgents.Length)]);
                req.Headers.Add("Accept-Language", "en-US,en;q=0.9");
                req.Headers.Add("Accept-Encoding", "gzip, deflate, br");
                
                var reqStopwatch = Stopwatch.StartNew();
                try
                {
                    using var res = await _client.SendAsync(req);
                    reqStopwatch.Stop();
                    long duration = reqStopwatch.ElapsedMilliseconds;
                    latencies.Add(duration);

                    string action = ParseAction(res, duration);
                    actionCounts[action]++;
                }
                catch
                {
                    actionCounts["Block"]++;
                }
            }
            stopwatch.Stop();

            return CalculateResult(total, stopwatch.Elapsed.TotalSeconds, latencies, actionCounts);
        }

        private static Dictionary<string, int> InitializeActionCounts()
        {
            return new Dictionary<string, int>
            {
                { "Allow", 0 },
                { "Block", 0 },
                { "Throttle", 0 },
                { "Challenge", 0 },
                { "Tarpit", 0 }
            };
        }

        private static string ParseAction(HttpResponseMessage res, long durationMs)
        {
            if (res.StatusCode == System.Net.HttpStatusCode.OK)
            {
                return "Allow";
            }
            if (res.StatusCode == (System.Net.HttpStatusCode)429)
            {
                return "Throttle";
            }
            if (res.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                try
                {
                    string content = res.Content.ReadAsStringAsync().Result;
                    if (content.Contains("\"Challenge\":true") || content.Contains("\"challenge\":true"))
                    {
                        return "Challenge";
                    }
                }
                catch { }

                if (durationMs >= 4500)
                {
                    return "Tarpit";
                }

                return "Block";
            }

            return "Allow";
        }

        private static ScenarioResult CalculateResult(int total, double elapsedSeconds, List<long> latencies, Dictionary<string, int> actionCounts)
        {
            var sortedLatencies = latencies.OrderBy(x => x).ToList();
            double p50 = sortedLatencies.Count > 0 ? sortedLatencies[(int)(sortedLatencies.Count * 0.50)] : 0;
            double p95 = sortedLatencies.Count > 0 ? sortedLatencies[(int)(sortedLatencies.Count * 0.95)] : 0;
            double p99 = sortedLatencies.Count > 0 ? sortedLatencies[(int)(sortedLatencies.Count * 0.99)] : 0;

            return new ScenarioResult
            {
                TotalRequests = total,
                Rps = total / elapsedSeconds,
                P50 = p50,
                P95 = p95,
                P99 = p99,
                BlockedCount = actionCounts["Block"],
                ThrottledCount = actionCounts["Throttle"],
                ChallengedCount = actionCounts["Challenge"],
                TarpittedCount = actionCounts["Tarpit"],
                ActionCounts = actionCounts
            };
        }
    }

    class ScenarioResult
    {
        public int TotalRequests { get; set; }
        public double Rps { get; set; }
        public double P50 { get; set; }
        public double P95 { get; set; }
        public double P99 { get; set; }
        public int BlockedCount { get; set; }
        public int ThrottledCount { get; set; }
        public int ChallengedCount { get; set; }
        public int TarpittedCount { get; set; }
        public Dictionary<string, int> ActionCounts { get; set; } = new();
    }
}
