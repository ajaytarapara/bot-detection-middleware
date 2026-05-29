using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BotDetector.Business.Models
{
    // Instead of every rule reading directly from: HttpContext, we convert HttpContext once into: BotRequestContext
    public class BotRequestContext
    {
        public string IpAddress { get; set; } = string.Empty;

        public string UserAgent { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;

        public string Method { get; set; } = string.Empty;

        public Dictionary<string, string> Headers { get; set; } = new();

        public DateTime RequestTimeUtc { get; set; }

        public string? ApiKey { get; set; }

        public string? Fingerprint { get; set; }
    }
}