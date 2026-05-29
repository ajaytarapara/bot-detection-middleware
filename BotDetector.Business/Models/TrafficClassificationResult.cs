using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BotDetector.Common.Enums;

namespace BotDetector.Business.Models
{
    public class TrafficClassificationResult
    {
        public TrafficType TrafficType { get; set; }

        public bool BypassDetection { get; set; }

        public string Reason { get; set; } = string.Empty;
    }
}