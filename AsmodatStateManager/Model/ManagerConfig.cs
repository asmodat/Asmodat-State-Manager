using Microsoft.AspNetCore.Mvc;
using System;
using AsmodatStandard.Extensions;
using AsmodatStandard.Extensions.Collections;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using AsmodatStandard.Extensions.IO;
using System.Collections.Generic;
using AsmodatStandard.Types;

namespace AsmodatStateManager.Model
{
    public class ManagerConfig
    {
        public string version { get; set; } = "0.1.4";
        public string login { get; set; }
        public string password { get; set; }

        public string welcomeMessage { get; set; } = "Welcome To Asmodat State Manager";

        public int defaultHttpClientTimeout { get; set; } = 5;

        public int diskCheckIntensity { get; set; } = 2500;
        public int syncIntensity { get; set; } = 1000;

        /// <summary>
        /// Defines which disks cause cause health check triggers above which usage %
        /// eg: { "c": 90, d: "70"  }
        /// </summary>
        public Dictionary<string, float> diskHealthChecks { get; set; }
        public int parallelism { get; set; } = 1;

        public string targets { get; set; }

        public SyncTarget[] GetSyncTargets()
        {
            var direcotry = targets.ToDirectoryInfo();
            if (!direcotry.TryCreate())
                throw new Exception($"Target directory: '{targets}' does not exist and coouldn't be created.");

            var files = direcotry.GetFiles();

            var syncTargets = new List<SyncTarget>();
            foreach (var f in files)
            {
                if (f.Length <= 0 || f.Extension.ToLower().Trim(".") != "json")
                    continue;

                var st = f.ReadAllText().JsonDeserialize<SyncTarget>();
                if (st == null)
                    continue;

                st.path = f.FullName;
                syncTargets.Add(st);
            }

            return syncTargets.ToArray();
        }
    }

    public class StatusFile
    {
        public DateTime GetDateTime() => timestamp.ToDateTimeFromUnixTimestamp();

        public string bucket { get; set; }
        public string key { get; set; }
        public string location { get; set; }
        public string id { get; set; }
        

        public long timestamp { get; set; }
        public ulong version { get; set; } = 0;


        public bool finalized { get; set; } = false;
        public long intensity = 0;
        public SyncTarget target { get; set; }
        public SilyFileInfo[] files { get; set; }
        public SilyDirectoryInfo[] directories { get; set; }
        public string[] obsoletes { get; set; }

        public string source { get; set; }
        public string destination { get; set; }

        /// <summary>
        /// Defines how many times file was synced sucesfully
        /// </summary>
        public long counter { get; set; } = 0;
    }
}
