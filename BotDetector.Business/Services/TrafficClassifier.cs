using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using BotDetector.Business.Configurations;
using BotDetector.Business.Interfaces;
using BotDetector.Business.Models;
using BotDetector.Common.Enums;
using Microsoft.Extensions.Options;

namespace BotDetector.Business.Services
{
    public class TrafficClassifier : ITrafficClassifier
    {
        private readonly TrafficClassificationOptions _options;

        public TrafficClassifier(IOptions<TrafficClassificationOptions> options)
        {
            _options = options.Value;
        }

        public async Task<TrafficClassificationResult> ClassifyAsync(BotRequestContext context)
        {
            // 1. Path-based Whitelisting (Internal Services / Health check / Webhooks)
            if (_options.InternalPaths.Any(p => IsPathMatch(context.Path, p)))
            {
                return new TrafficClassificationResult
                {
                    TrafficType = TrafficType.InternalService,
                    BypassDetection = true,
                    Reason = "Internal path bypass"
                };
            }

            // 2. IP Blocklist (Known Abusers)
            if (_options.BlockedIps.Any(ip => IsIpInCidr(context.IpAddress, ip)))
            {
                return new TrafficClassificationResult
                {
                    TrafficType = TrafficType.KnownAbuser,
                    BypassDetection = false,
                    Reason = "IP matches blocklist"
                };
            }

            // 3. IP Whitelist (Trusted office / partners)
            if (_options.TrustedIps.Any(ip => IsIpInCidr(context.IpAddress, ip)))
            {
                return new TrafficClassificationResult
                {
                    TrafficType = TrafficType.Human,
                    BypassDetection = true,
                    Reason = "IP matches trusted whitelist"
                };
            }

            // 4. API Key Verification (API Partners)
            if (context.Headers.TryGetValue(_options.ApiKeyHeader, out var apiKey) && !string.IsNullOrWhiteSpace(apiKey))
            {
                if (_options.TrustedApiKeys.Contains(apiKey))
                {
                    return new TrafficClassificationResult
                    {
                        TrafficType = TrafficType.ApiPartner,
                        BypassDetection = true,
                        Reason = "Authenticated API partner"
                    };
                }
            }

            // 5. Verified Bots (Googlebot, Bingbot, etc. - Authenticity verification via DNS)
            string userAgent = context.UserAgent ?? string.Empty;
            if (_options.VerifiedBotUserAgents.Any(b => userAgent.Contains(b, StringComparison.OrdinalIgnoreCase)))
            {
                bool isAuthentic = await VerifyBotAuthenticityAsync(context.IpAddress, userAgent);
                if (isAuthentic)
                {
                    return new TrafficClassificationResult
                    {
                        TrafficType = TrafficType.VerifiedBot,
                        BypassDetection = true,
                        Reason = "Verified authentic search crawler"
                    };
                }
                else
                {
                    // Spoofed bot detection - flag as potential bot
                    return new TrafficClassificationResult
                    {
                        TrafficType = TrafficType.Unknown,
                        BypassDetection = false,
                        Reason = "Spoofed crawler user-agent detected"
                    };
                }
            }

            // 6. Anonymous / Standard users
            return new TrafficClassificationResult
            {
                TrafficType = TrafficType.Unknown,
                BypassDetection = false,
                Reason = "Anonymous traffic"
            };
        }

        private bool IsPathMatch(string path, string pattern)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(pattern)) return false;

            if (pattern.EndsWith("/*"))
            {
                var prefix = pattern.Substring(0, pattern.Length - 2);
                return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }

            return path.Equals(pattern, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsIpInCidr(string ipAddress, string cidr)
        {
            if (string.IsNullOrEmpty(ipAddress) || string.IsNullOrEmpty(cidr)) return false;
            
            if (ipAddress == cidr) return true;

            if (!cidr.Contains('/'))
            {
                return ipAddress == cidr;
            }

            try
            {
                var parts = cidr.Split('/');
                if (parts.Length != 2) return false;

                var cidrIp = IPAddress.Parse(parts[0]);
                int cidrMask = int.Parse(parts[1]);

                // Strip port if present in IP address (e.g. 197.0.0.1:5000 -> 197.0.0.1)
                string rawIp = ipAddress;
                int colonIndex = ipAddress.LastIndexOf(':');
                if (colonIndex > 0 && ipAddress.Count(c => c == ':') == 1) // Simple IPv4:port
                {
                    rawIp = ipAddress.Substring(0, colonIndex);
                }

                var clientIp = IPAddress.Parse(rawIp);

                if (cidrIp.AddressFamily != clientIp.AddressFamily) return false;

                byte[] cidrBytes = cidrIp.GetAddressBytes();
                byte[] clientBytes = clientIp.GetAddressBytes();

                int totalBits = cidrBytes.Length * 8;
                if (cidrMask < 0 || cidrMask > totalBits) return false;

                int remainingBits = cidrMask;
                for (int i = 0; i < cidrBytes.Length; i++)
                {
                    if (remainingBits >= 8)
                    {
                        if (cidrBytes[i] != clientBytes[i]) return false;
                        remainingBits -= 8;
                    }
                    else if (remainingBits > 0)
                    {
                        byte mask = (byte)(0xFF << (8 - remainingBits));
                        if ((cidrBytes[i] & mask) != (clientBytes[i] & mask)) return false;
                        break;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> VerifyBotAuthenticityAsync(string ipAddress, string userAgent)
        {
            if (string.IsNullOrEmpty(ipAddress)) return false;

            bool isGoogle = userAgent.Contains("Googlebot", StringComparison.OrdinalIgnoreCase);
            bool isBing = userAgent.Contains("Bingbot", StringComparison.OrdinalIgnoreCase);

            if (!isGoogle && !isBing) return false;

            try
            {
                // Strip port from IP address if it exists
                string rawIp = ipAddress;
                int colonIndex = ipAddress.LastIndexOf(':');
                if (colonIndex > 0 && ipAddress.Count(c => c == ':') == 1)
                {
                    rawIp = ipAddress.Substring(0, colonIndex);
                }

                var ip = IPAddress.Parse(rawIp);
                
                // Step 1: Reverse DNS Lookup
                var hostEntry = await Dns.GetHostEntryAsync(ip);
                var hostName = hostEntry.HostName;

                // Step 2: Validate Domain
                bool domainValid = false;
                if (isGoogle && (hostName.EndsWith(".googlebot.com", StringComparison.OrdinalIgnoreCase) || 
                                 hostName.EndsWith(".google.com", StringComparison.OrdinalIgnoreCase)))
                {
                    domainValid = true;
                }
                else if (isBing && hostName.EndsWith(".search.msn.com", StringComparison.OrdinalIgnoreCase))
                {
                    domainValid = true;
                }

                if (!domainValid) return false;

                // Step 3: Forward DNS Lookup to verify it resolves back to the same IP
                var forwardEntry = await Dns.GetHostAddressesAsync(hostName);
                return forwardEntry.Any(a => a.Equals(ip));
            }
            catch
            {
                // If DNS lookup fails (no network, timed out), default to false to prevent spoofing
                return false;
            }
        }
    }
}