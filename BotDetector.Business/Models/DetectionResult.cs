using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BotDetector.Common.Enums;

namespace BotDetector.Business.Models
{
    public class DetectionResult
    {
        public int TotalScore { get; set; }

        public BotAction Action { get; set; }

        public List<string> Reasons { get; set; } = new();

        public List<RuleResult> RuleResults { get; set; } = new();
    }
}