using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BotDetector.Business.Interfaces;
using BotDetector.Business.Models;
using BotDetector.Common.Enums;

namespace BotDetector.Business.Services
{
    public class TrafficClassifier : ITrafficClassifier
    {
        public Task<TrafficClassificationResult> ClassifyAsync(BotRequestContext context)
        {
            return Task.FromResult(
                new TrafficClassificationResult
                {
                    TrafficType = TrafficType.Unknown,
                    BypassDetection = false,
                    Reason = string.Empty
                });
        }
    }
}