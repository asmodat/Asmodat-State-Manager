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
using AWSWrapper.S3;
using Amazon.SecurityToken.Model;
using Amazon;
using System.Threading.Tasks;
using AsmodatStandard.Extensions.IO;
using AsmodatStandard.Extensions.Threading;
using AsmodatStateManager.Model;
using Amazon.Runtime;
using Amazon.S3;
using AsmodatStandard.Cryptography;
using System.Collections.Concurrent;
using AsmodatStandard.Extensions.Collections;
using AsmodatStandard.Types;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;

namespace AsmodatStateManager.Processing
{
    public class SyncResult
    {
        public SyncResult(bool success = false, double speed = double.NaN, long duration = 0, string error = null, long run = 0)
        {
            this.run = run;
            this.success = success;
            this.speed = speed;
            this.duration = duration;
            this.error = error;
        }

        public long run { get; set; }
        public bool success { get; set; }
        public double speed { get; set; }
        public long duration { get; set; }
        public string error { get; set; }
    }
}
