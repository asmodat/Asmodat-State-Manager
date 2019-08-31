using System;
using AsmodatStandard.Extensions;
using AsmodatStandard.Extensions.Collections;
using Microsoft.Extensions.Options;
using AsmodatStateManager.Model;
using System.Linq;

using AWSWrapper.S3;
using System.Threading.Tasks;
using AsmodatStandard.Extensions.Threading;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace AsmodatStateManager.Processing
{
    public partial class SyncManager
    {
        private readonly ManagerConfig _cfg;
        private S3Helper _S3Helper;

        private ConcurrentDictionary<string, SyncInfo> _syncInfo;
        private ConcurrentDictionary<string, SyncResult> _syncResult;
        public readonly static object _locker = new object();
        private long _run = 0;

        public SyncManager(IOptions<ManagerConfig> cfg)
        {
            _cfg = cfg.Value;
        }

        public async Task Process()
        {
            var syncTargets = _cfg.GetSyncTargets();
            if (syncTargets.IsNullOrEmpty())
                return;

            foreach (var st in syncTargets)
            {
                if (st.source.IsNullOrEmpty())
                    throw new Exception("SyncTarget 'source' was not defined");
                if (st.destination.IsNullOrEmpty())
                    throw new Exception("SyncTarget 'destination' was not defined");

                if (st.id.IsNullOrEmpty())
                    st.id = Guid.NewGuid().ToString();
            }

            if (_syncResult == null || _syncResult.Count != syncTargets.Length || syncTargets.Any(x => !_syncResult.ContainsKey(x.id)))
            {
                _syncResult = new ConcurrentDictionary<string, SyncResult>();
                foreach (var st in syncTargets)
                    _syncResult.Add(st.id, null);
            }

            if (_syncInfo == null || _syncResult.Count != syncTargets.Length || syncTargets.Any(x => !_syncInfo.ContainsKey(x.id)))
            {
                _syncInfo = new ConcurrentDictionary<string, SyncInfo>();
                foreach (var st in syncTargets)
                    _syncInfo.Add(st.id, null);
            }

            ++_run;
            await ParallelEx.ForEachAsync(syncTargets, async st => {
                var sw = Stopwatch.StartNew();

                if (st.type == SyncTarget.types.none)
                    return;

                SyncResult result;
                if (st.type == SyncTarget.types.awsUpload)
                {
                    /* // debug only
                    result = await UploadAWS(st);
                    /*/
                    result = TryProcessUploadAWS(st);
                    //*/
                }
                else if (st.type == SyncTarget.types.awsDownload)
                {
                    /* // debug only
                    result = await DownloadAWS(st);
                    /*/
                    result = TryProcessDownloadAWS(st);
                    //*/
                }
                else
                    throw new Exception($"SyncTarget type '{st.type.ToString()}' was not defined");

                result.run = _run;
                result.duration = sw.ElapsedMilliseconds / 1000;
                _syncResult[st.id] = result;

                await Task.Delay(1000); //rate limiting potential errors

            }, maxDegreeOfParallelism: _cfg.parallelism);
        }

        public SyncResult TryProcessUploadAWS(SyncTarget st)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var result = this.UploadAWS(st).Result;
                return result;
            }
            catch (Exception ex)
            {
                var exception = ex.JsonSerializeAsPrettyException();
                Console.WriteLine($"Failed to TryProcessUploadAWS, Error Message: {exception}");
                Thread.Sleep(1000); //rate limiting potential errors
                return new SyncResult(error: exception);
            }
        }

        public SyncResult TryProcessDownloadAWS(SyncTarget st)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var result = this.DownloadAWS(st).Result;
                return result;
            }
            catch (Exception ex)
            {
                var exception = ex.JsonSerializeAsPrettyException();
                Console.WriteLine($"Failed to TryProcessDownloadAWS, Error Message: {exception}");
                Thread.Sleep(1000); //rate limiting potential errors
                return new SyncResult(error: exception);
            }
        }

        public SyncStatus GetSyncStatus() => new SyncStatus(_syncInfo.DeepCopy().ToDictionary(), _syncResult.DeepCopy().ToDictionary(), _run);
    }
}
