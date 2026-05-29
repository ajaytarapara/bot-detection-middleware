using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BotDetector.Business.Models;

namespace BotDetector.Business.Interfaces
{
    public interface IDetectionEngine
    {
        Task<DetectionResult> AnalyzeAsync(BotRequestContext context);
    }
}