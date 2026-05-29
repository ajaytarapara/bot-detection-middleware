using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BotDetector.API.Middleware
{
    public static class MiddlewareExtensions
    {
        public static IApplicationBuilder UseBotDetection(this IApplicationBuilder app)
        {
            return app.UseMiddleware<BotDetectionMiddleware>();
        }
    }
}