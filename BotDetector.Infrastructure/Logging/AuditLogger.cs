using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BotDetector.Business.Interfaces;
using BotDetector.Business.Models;
using Microsoft.Extensions.Logging;

namespace BotDetector.Infrastructure.Logging
{
    public class AuditLogger : IAuditLogger
    {
        private readonly ILogger<AuditLogger> _logger;

        public AuditLogger(
            ILogger<AuditLogger> logger)
        {
            _logger = logger;
        }

        public Task LogAsync(RequestAudit audit)
        {
            _logger.LogInformation(
                "Bot Audit - IP={IpAddress} Path={Path} Score={Score} Action={Action} Reasons={Reasons}",
                audit.IpAddress,
                audit.Path,
                audit.Score,
                audit.Action,
                string.Join(", ", audit.Reasons));

            return Task.CompletedTask;
        }
    }
}