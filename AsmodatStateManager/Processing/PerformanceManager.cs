#define LINUX

using System;
using AsmodatStandard.Extensions;
using AsmodatStandard.Extensions.Collections;
using Microsoft.Extensions.Options;
using AsmodatStateManager.Model;
using System.Linq;

#if WINDOWS
        using System.Diagnostics;
#endif

using System.IO;
using AsmodatStateManager.Ententions;

namespace AsmodatStateManager.Processing
{
    public class PerformanceManager
    {
        private readonly ManagerConfig _cfg;
        private DiskInfo[] driveInfo;

        public PerformanceManager(IOptions<ManagerConfig> cfg)
        {
            _cfg = cfg.Value;
        }

        public void TryUpdateDriveInfo()
        {
            try
            {
                driveInfo = DriveInfo.GetDrives().Select(x => x.ToDiskInfo()).Where(x => x != null).ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to TryUpdateDriveInfo, Error Message: {ex.JsonSerializeAsPrettyException()}");
            }
        }

        public DiskInfo[] GetDriveInfo() => driveInfo;
    }
}
