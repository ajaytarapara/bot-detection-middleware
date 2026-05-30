using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BotDetector.Business.Configurations
{
    public class DetectionThresholds
    {
        public int AllowMax { get; set; } = 30;
        public int ShadowMax { get; set; } = 50;
        public int ChallengeMax { get; set; } = 70;
        public int ThrottleMax { get; set; } = 90;
        public int BlockMax { get; set; } = 100;
    }
}