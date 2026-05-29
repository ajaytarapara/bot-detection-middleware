using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BotDetector.Business.Models
{
    public class RequestAudit
    {
        public string IpAddress { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;

        public string Method { get; set; } = string.Empty;

        public int Score { get; set; }

        public string Action { get; set; } = string.Empty;

        public List<string> Reasons { get; set; } = new();

        public DateTime TimestampUtc { get; set; }
    }
}