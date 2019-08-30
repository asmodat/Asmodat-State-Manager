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
    public class SyncService : BackgroundService
    {
        private readonly ManagerConfig _cfg;
        private SyncManager _sm;
        private DateTime timestamp;

        public SyncService(IOptions<ManagerConfig> cfg, SyncManager sm) 
        {
            _sm = sm;
            _cfg = cfg.Value;
            timestamp = DateTime.UtcNow;
        }


        protected override async Task Process()
        {
            var delay = 10;
            if ((DateTime.UtcNow - timestamp).TotalSeconds < _cfg.syncIntensity)
            {
                var timeUntilNextExecution = _cfg.syncIntensity - (DateTime.UtcNow - timestamp).TotalMilliseconds;
                if (timeUntilNextExecution > delay)
                    delay = (int)timeUntilNextExecution;
            }

            await Task.Delay(delay);
            await _sm.Process();
            timestamp = DateTime.UtcNow;
        }
    }
}
