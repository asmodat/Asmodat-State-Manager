#define LINUX

using System;
using AsmodatStandard.Extensions;
using AsmodatStandard.Extensions.Collections;
using AsmodatStateManager.Model;
using System.Linq;

#if WINDOWS
        using System.Diagnostics;
#endif

using AWSWrapper.S3;
using System.Threading.Tasks;
using AsmodatStandard.Extensions.IO;
using AsmodatStandard.Extensions.Threading;
using AsmodatStandard.Cryptography;
using System.Collections.Concurrent;
using AsmodatStandard.Types;
using System.Collections.Generic;
using System.Threading;
using Amazon.S3.Model;

namespace AsmodatStateManager.Processing
{
    public partial class SyncManager
    {
        public readonly string UploadStatusFilePrefix = "sync-file-upload-";
        public readonly string DownloadStatusFilePrefix = "sync-file-download-";

        /// <summary>
        /// Returns list of s3 status files in ascending order (from oldest to latest)
        /// </summary>
        /// <param name="st"></param>
        /// <returns></returns>
        public async Task<List<S3Object>> GetStatusList(SyncTarget st, string statusPrefix)
        {
            var cts = new CancellationTokenSource();

            var bkp = st.status.ToBucketKeyPair();
            var prefix = $"{bkp.key}/{statusPrefix}";
            var list = (await _S3Helper.ListObjectsAsync(bkp.bucket, prefix, msTimeout: st.timeout, cancellationToken: cts.Token)
                .TryCatchRetryAsync(maxRepeats: st.retry))
                .SortAscending(x => x.Key.TrimStart(prefix).TrimEnd(".json").ToLongOrDefault(0)).ToList();

            if (cts.IsCancellationRequested)
                return null;

            return list;
        }

        public async Task<StatusFile> GetStatusFile(SyncTarget st, long minTimestamp, long maxTimestamp)
        {
            var bkp = st.status.ToBucketKeyPair();
            var prefix = $"{bkp.key}/{UploadStatusFilePrefix}";
            var list = await GetStatusList(st, UploadStatusFilePrefix);

            if (list.IsNullOrEmpty())
                return null;

            list.Reverse();
            foreach (var f in list) //find non obsolete files
            {
                var timestamp = f.Key.TrimStart(prefix).TrimEnd(".json").ToLongOrDefault(0);

                if (timestamp < minTimestamp || timestamp > maxTimestamp)
                    continue;

                var s = await _S3Helper.DownloadJsonAsync<StatusFile>(bkp.bucket, f.Key, throwIfNotFound: false)
                    .Timeout(msTimeout: st.timeout)
                    .TryCatchRetryAsync(maxRepeats: st.retry);

                if (s?.finalized == true)
                    return s;
            }

            return null;
        }

        public async Task<StatusFile> GetStatusFile(SyncTarget st, string statusPrefix)
        {
            var bkp = st.status.ToBucketKeyPair();
            var prefix = $"{bkp.key}/{UploadStatusFilePrefix}";
            var list = await GetStatusList(st, UploadStatusFilePrefix);

            var id = list.IsNullOrEmpty() ? 0 : list.Last().Key.TrimStart(prefix).TrimEnd(".json").ToLongOrDefault(0); //latest staus id
            id = id <= 0 ? DateTimeEx.TimestampNow() : id;
            var key = $"{prefix}{id}.json";

            if (list.IsNullOrEmpty() || id <= 0)
            {
                return new StatusFile()
                {
                    id = id.ToString(),
                    timestamp = DateTimeEx.UnixTimestampNow(),
                    bucket = bkp.bucket,
                    key = key,
                    location = $"{bkp.bucket}/{key}",
                    finalized = false,
                    version = 0
                };
            }

            var status = await _S3Helper.DownloadJsonAsync<StatusFile>(bkp.bucket, key, throwIfNotFound: true)
                .Timeout(msTimeout: st.timeout)
                .TryCatchRetryAsync(maxRepeats: st.retry);

            var counter = status.counter; //defines how many times sync target was saved sucesfully
            var elapsed = (DateTime.UtcNow - long.Parse(status?.id ?? "0").ToDateTimeFromTimestamp()).TotalSeconds;
            if(status == null || status.finalized == true && st.retention > 0 && elapsed > st.retention)
            {
                id = DateTimeEx.TimestampNow();
                key = $"{prefix}{id}.json";
                status = new StatusFile()
                {
                    id = id.ToString(),
                    timestamp = DateTimeEx.UnixTimestampNow(),
                    bucket = bkp.bucket,
                    key = key,
                    location = $"{bkp.bucket}/{key}",
                    finalized = false,
                    version = 0,
                    counter = counter
                };
            }

            if(st.rotation > 0 && list.Count > st.rotation)
            {
                var validStatus = new List<StatusFile>();
                list.Reverse();
                foreach (var f in list) //find non obsolete files
                {
                    var s = await _S3Helper.DownloadJsonAsync<StatusFile>(bkp.bucket, f.Key, throwIfNotFound: false)
                        .Timeout(msTimeout: st.timeout)
                        .TryCatchRetryAsync(maxRepeats: st.retry);

                    if (s == null)
                        continue;

                    if (s.finalized && s.id.ToLongOrDefault(0) > 0)
                        validStatus.Add(s);
                    else if(!s.finalized || status.id == id.ToString())
                        validStatus.Add(s);

                    if (validStatus.Count > st.rotation)
                        break;
                }

                status.obsoletes = list.Where(x => !validStatus.Any(v => v.key.ToLower().Trim() == x.Key.ToLower().Trim()))
                    .Select(x => x.Key).ToArray(); //status files that are obsolete
            }

            return status;
        }
    }
}
