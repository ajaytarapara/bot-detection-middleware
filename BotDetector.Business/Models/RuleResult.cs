using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BotDetector.Business.Models
{
    // Each detection rule returns its own result.
    public class RuleResult
    {
        public string RuleName { get; set; } = string.Empty;

        public int Score { get; set; }

        public bool IsSuspicious { get; set; }

        public string Reason { get; set; } = string.Empty;
    }
}