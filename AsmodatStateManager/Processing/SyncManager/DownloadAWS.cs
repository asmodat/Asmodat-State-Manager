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
    public partial class SyncManager
    {
        public async Task<SyncResult> DownloadAWS(SyncTarget st)
        {
            _S3Helper = st.profile.IsNullOrEmpty() ?
                new S3Helper() :
                new S3Helper(AWSWrapper.Extensions.Helper.GetAWSCredentials(st.profile));

            var bkp = st.source.ToBucketKeyPair();
            var bucket = bkp.bucket;
            var timestamp = DateTimeEx.UnixTimestampNow();

            if (bucket.IsNullOrEmpty())
                throw new Exception($"Source '{st.source ?? "undefined"}' does not contain bucket name.");

            var destination = st.destination?.ToDirectoryInfo();

            if (destination?.TryCreate() != true)
                throw new Exception($"Destination '{st.destination ?? "undefined"}' does not exist and coudn't be created.");

            var status = await GetStatusFile(st, st.minTimestamp, st.maxTimestamp);
            var downloadStatus = await GetStatusFile(st, DownloadStatusFilePrefix);

            if (status == null)
                throw new Exception($"Could not download latest data from the source '{st.source}', status file was not found in '{st?.status ?? "undefined"}' within time range of <{st.minTimestamp.ToDateTimeFromTimestamp().ToLongDateTimeString()},{st.maxTimestamp.ToDateTimeFromTimestamp().ToLongDateTimeString()}>");

            if (st.maxSyncCount > 0 && downloadStatus.counter >= st.maxSyncCount) //maximum number of syncs is defined
            {
                Console.WriteLine($"Download sync file '{st.status}' was already finalized maximum number of {st.maxSyncCount}");
                await Task.Delay(millisecondsDelay: 1000);
                return new SyncResult(success: true);
            }

            var elspased = DateTimeEx.UnixTimestampNow() - downloadStatus.timestamp;
            if (downloadStatus.finalized && elspased < st.intensity)
            {
                var remaining = st.intensity - elspased;
                var delay = Math.Min(Math.Max((remaining * 1000) >= int.MaxValue ? 0 : (int)(remaining - 1000), 0), 1000);
                Console.WriteLine($"Download sync file '{st.status}' was finalized {elspased}s ago. Next sync in {st.intensity - elspased}s.");
                await Task.Delay(millisecondsDelay: (int)(delay * 1000));
                return new SyncResult(success: true);
            }

            _syncInfo[st.id] = new SyncInfo(st);
            _syncInfo[st.id].total = status.files.Sum(x => x.Length);
            _syncInfo[st.id].timestamp = timestamp;

            int counter = 0;
            var directories = new List<DirectoryInfo>();
            directories.Add(st.destination.ToDirectoryInfo());
            foreach (var dir in status.directories)
            {
                var relativeDir = dir.FullName.TrimStart(status.source);
                var downloadDir = PathEx.RuntimeCombine(st.destination, relativeDir).ToDirectoryInfo();

                if (!downloadDir.Exists && st.verbose >= 1)
                    Console.WriteLine($"Creating Directory [{++counter}/{status.directories.Length}] '{downloadDir.FullName}' ...");

                if (downloadDir?.TryCreate() != true)
                    throw new Exception($"Could not find or create directory '{downloadDir?.FullName ?? "undefined"}'.");

                directories.Add(downloadDir);
            }

            if (st.wipe)
            {
                counter = 0;
                var currentDirectories = st.destination.ToDirectoryInfo().GetDirectories(recursive: st.recursive);
                foreach (var dir in currentDirectories)
                    if (!directories.Any(x => x.FullName == dir.FullName))
                    {
                        Console.WriteLine($"Removing Directory [{++counter}/{currentDirectories.Length - directories.Count}] '{dir.FullName}' ...");
                        dir.Delete(recursive: st.recursive);
                    }
            }

            counter = 0;
            var files = new List<FileInfo>();
            var speedList = new List<double>();
            await ParallelEx.ForEachAsync(status.files, async file =>
            {
                try
                {
                    var relativePath = file.FullName.TrimStart(status.source);
                    var downloadPath = PathEx.RuntimeCombine(st.destination, relativePath).ToFileInfo();
                    files.Add(downloadPath);

                    if (downloadPath.Exists && downloadPath.MD5().ToHexString() == file.MD5)
                        return; //file already exists

                    if (downloadPath.Exists && downloadPath.TryDelete() != true)
                        throw new Exception($"Obsolete file was found in '{downloadPath?.FullName ?? "undefined"}' but couldn't be deleted.");

                    var key = $"{st.source.TrimEnd('/')}/{status.id}/{relativePath.Trim("/").ToLinuxPath().Trim("/")}"
                                .TrimStartSingle(bucket).Trim("/");

                    ++counter;
                    if (st.verbose >= 1) Console.WriteLine($"Downloading [{counter}/{status.files.Length}][{file.Length}B] '{bucket}/{key}' => '{downloadPath.FullName}' ...");

                    var sw = Stopwatch.StartNew();
                    var stream = await _S3Helper.DownloadObjectAsync(bucketName: bucket, key: key, throwIfNotFound: true)
                                                .Timeout(msTimeout: st.timeout);

                    if (!downloadPath.Directory.TryCreate())
                        throw new Exception($"Failed to create directory '{downloadPath?.Directory.FullName ?? "undefined"}'.");

                    using (var fs = File.Create(downloadPath.FullName))
                        stream.CopyTo(fs);

                    downloadPath.Refresh();
                    if (!downloadPath.Exists)
                        throw new Exception($"Failed download '{bucket}/{key}'-/-> '{downloadPath.FullName}'.");

                    if (st.verify)
                    {
                        var md5 = downloadPath.MD5().ToHexString();
                        if (md5 != file.MD5)
                            throw new Exception($"Failed download '{bucket}/{key}'-/-> '{downloadPath.FullName}', expected MD5 to be '{md5 ?? "undefined"}' but was '{file.MD5 ?? "undefined"}'.");
                        else
                            lock (_locker)
                            {
                                var megabytes = (double)(file.Length + (md5.Length + bucket.Length + key.Length) * sizeof(char)) / (1024 * 1024);
                                var seconds = (double)(sw.ElapsedMilliseconds + 1) / 1000;
                                var speed = megabytes / seconds;
                                speedList.Add(speed);
                            }
                    }
                }
                finally
                {
                    _syncInfo[st.id].processed += file.Length;
                    _syncInfo[st.id].progress = ((double)_syncInfo[st.id].processed / _syncInfo[st.id].total) * 100;
                }
            }, maxDegreeOfParallelism: st.parallelism);

            if (st.wipe)
            {
                counter = 0;
                var currentFiles = st.destination.ToDirectoryInfo().GetFiles("*", recursive: st.recursive);
                foreach (var file in currentFiles)
                    if (!files.Any(x => x.FullName == file.FullName))
                    {
                        Console.WriteLine($"Removing File [{++counter}/{currentFiles.Length - files.Count}] '{file.FullName}' ...");
                        file.Delete();
                    }
            }

            downloadStatus.finalized = true;
            downloadStatus.counter += 1;
            var uploadResult = await _S3Helper.UploadJsonAsync(downloadStatus.bucket, downloadStatus.key, downloadStatus)
                .Timeout(msTimeout: st.timeout)
                .TryCatchRetryAsync(maxRepeats: st.retry);

            var avgSpeed = speedList.IsNullOrEmpty() ? double.NaN : speedList.Average();
            Console.WriteLine($"SUCCESS, processed '{st.status}', all {status.files.Length} files and {status.directories.Length} directories were updated.");
            Console.WriteLine($"Average Download Speed: {avgSpeed} MB/s");
            return new SyncResult(success: true, speed: avgSpeed);
        }
    }
}
