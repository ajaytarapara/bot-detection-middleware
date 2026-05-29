using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BotDetector.Business.Interfaces;
using BotDetector.Business.Models;
using Microsoft.AspNetCore.Mvc;

namespace BotDetector.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DebugController : ControllerBase
    {
        private readonly IDetectionEngine _engine;

        public DebugController(
            IDetectionEngine engine)
        {
            _engine = engine;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var context = new BotRequestContext
            {
                IpAddress = "127.0.0.1",
                UserAgent = "",
                Path = "/api/debug",
                Method = "GET",
                Headers = new Dictionary<string, string>()
            };

            var result =
                await _engine.AnalyzeAsync(context);

            return Ok(result);
        }
    }
}