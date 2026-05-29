using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BotDetector.Business.Configurations
{
    public class DetectionThresholds
    {
        public int AllowMax { get; set; }

        public int ShadowMax { get; set; }

        public int ChallengeMax { get; set; }

        public int ThrottleMax { get; set; }

        public int BlockMax { get; set; }
    }
}