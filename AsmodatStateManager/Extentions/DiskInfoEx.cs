using System;
using System.IO;
using AsmodatStandard.Extensions;
using AsmodatStateManager.Model;

namespace AsmodatStateManager.Ententions
{
    public static class DiskInfoEx
    {
        public static DiskInfo ToDiskInfo(this DriveInfo di)
        {
            if (di == null)
                return null;

            var dName = di.Name?.ToLower().ReplaceMany((" ", ""), (":", ""), ("/", ""), ("\\", "")) ?? "undefined";
            var dType = di.DriveType.ToString();
            var dReady = di.IsReady;

            if(!di.IsReady)
            {
                return new DiskInfo()
                {
                    name = dName,
                    type = dType,
                    ready = dReady
                };
            }

            return new DiskInfo()
            {
                name = dName,
                type = dType,
                ready = dReady,
                label = di.VolumeLabel,
                format = di.DriveFormat,
                size = di.TotalSize,
                free = di.TotalSize <= 0 ? 0 : 
                    (float)((double)Math.Min(di.TotalFreeSpace, di.AvailableFreeSpace) / di.TotalSize) * 100,
            };
        }
    }
}
