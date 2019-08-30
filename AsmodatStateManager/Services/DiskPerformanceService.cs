using Microsoft.AspNetCore.Mvc;
using System;
using AsmodatStandard.Extensions;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using System.Net.Http;
using AsmodatStateManager.Model;
using System.Text;
using Microsoft.Extensions.Hosting;
using System.Threading;
using AsmodatStateManager.Processing;

namespace AsmodatStateManager.Services
{
    public class DiskPerformanceService : BackgroundService
    {
        private readonly ManagerConfig _cfg;
        private PerformanceManager _pm;
        private DateTime timestamp;

        public DiskPerformanceService(IOptions<ManagerConfig> cfg, PerformanceManager pm) 
        {
            _pm = pm;
            _cfg = cfg.Value;
            timestamp = DateTime.UtcNow;
        }


        protected override async Task Process()
        {
            var delay = 10;
            if ((DateTime.UtcNow - timestamp).TotalSeconds < _cfg.diskCheckIntensity)
            {
                var timeUntilNextExecution = _cfg.diskCheckIntensity - (DateTime.UtcNow - timestamp).TotalMilliseconds;
                if (timeUntilNextExecution > delay)
                    delay = (int)timeUntilNextExecution;
            }

            await Task.Delay(delay);
            _pm.TryUpdateDriveInfo();
            timestamp = DateTime.UtcNow;
        }
    }
}
