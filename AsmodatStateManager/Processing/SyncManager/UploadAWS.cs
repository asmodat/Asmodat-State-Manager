﻿#define LINUX

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
using System.Diagnostics;

namespace AsmodatStateManager.Processing
{
    public partial class SyncManager
    {
        

        public async Task<bool> Cleanup(SyncTarget st, StatusFile sf)
        {
            if (sf.obsoletes.IsNullOrEmpty())
                return true;

            var bkp = st.status.ToBucketKeyPair();
            var prefix = $"{bkp.key}/{UploadStatusFilePrefix}";
            var success = true;

            await ParallelEx.ForEachAsync(sf.obsoletes, async file =>
            {
                var cts = new CancellationTokenSource();

                var id = file.TrimStart(prefix).TrimEnd(".json").ToLongOrDefault(0);
                var folderLocation = $"{st.destination.TrimEnd('/','\\')}/{id}";
                var folderBKP = folderLocation.ToBucketKeyPair();
                var result = await _S3Helper.DeleteDirectoryAsync(folderBKP.bucket, folderBKP.key, throwOnFailure: false, cancellationToken: cts.Token)
                                            .TryCancelAfter(cts.Token, msTimeout: st.timeout);
                lock (_locker)
                    success = success && result;
                if (result)
                {
                    result = await _S3Helper.DeleteObjectAsync(bucketName: folderBKP.bucket, key: file, throwOnFailure: false, cancellationToken: cts.Token)
                                            .TryCancelAfter(cts.Token, msTimeout: st.timeout);
                    lock (_locker)
                        success = success && result;
                }

                if(success)
                    Console.WriteLine($"Status file: '{folderBKP.bucket}/{file}' and coresponding directory '{folderBKP.bucket}/{folderBKP.key}' was removed.");
                else
                    Console.WriteLine($"Failed to remove status file: '{folderBKP.bucket}/{file}' or coresponding directory '{folderBKP.bucket}/{folderBKP.key}'.");

            }, maxDegreeOfParallelism: _cfg.parallelism);

            return success;
        }


        public async Task<SyncResult> UploadAWS(SyncTarget st)
        {
            _S3Helper = st.profile.IsNullOrEmpty() ? 
                new S3Helper() : 
                new S3Helper(AWSWrapper.Extensions.Helper.GetCredentials(st.profile));

            var bkp = st.destination.ToBucketKeyPair();
            var bucket = bkp.bucket;
            var key = bkp.key;
            var timestamp = DateTimeEx.UnixTimestampNow();

            if (bucket.IsNullOrEmpty())
                throw new Exception($"Destination '{st.destination ?? "undefined"}' does not contain bucket name.");

            var path = st.destination;
            var sourceInfo = st.GetSourceInfo();

            if (sourceInfo.rootDirectory == null)
                return new SyncResult(success: false); //failed to get source info

            var directory = st.source.ToDirectoryInfo();
            var prefix = directory.FullName;
            var counter = 0;

            var status = await GetStatusFile(st, UploadStatusFilePrefix);

            if (st.maxSyncCount > 0 && status.counter >= st.maxSyncCount) //maximum number of syncs is defined
            {
                Console.WriteLine($"Upload sync file '{st.status}' was already finalized maximum number of {st.maxSyncCount}");
                await Task.Delay(millisecondsDelay: 1000);
                return new SyncResult(success: true);
            }

            var elspased = DateTimeEx.UnixTimestampNow() - status.timestamp;
            if (status.finalized && elspased < st.intensity)
            {
                var remaining = st.intensity - elspased;
                var delay = Math.Min(Math.Max((remaining * 1000) >= int.MaxValue ? 0 : (int)(remaining - 1000), 0), 1000);
                Console.WriteLine($"Upload sync file '{st.status}' was finalized {elspased}s ago. Next sync in {st.intensity - elspased}s.");
                await Task.Delay(millisecondsDelay: (int)(delay * 1000));
                return new SyncResult(success: true);
            }

            _syncInfo[st.id] = new SyncInfo(st);
            _syncInfo[st.id].total = sourceInfo.files.Sum(x => x.Length);
            _syncInfo[st.id].timestamp = timestamp;

            var cleanupTask = Cleanup(st, status);

            var isStatusFileUpdated = false;
            var files = new ConcurrentBag<SilyFileInfo>();

            var speedList = new List<double>();
            await ParallelEx.ForEachAsync(sourceInfo.files, async file =>
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    var destination = $"{key}/{status.id}/" + file.FullName.TrimStartSingle(sourceInfo.rootDirectory.FullName)
                        .TrimStart('/', '\\')
                        .Replace("\\", "/");

                    var uploadedFile = status.files?.FirstOrDefault(x => x.FullName.ToLower().Trim() == file.FullName.ToLower().Trim());

                    if (uploadedFile != null) //file was already uploaded to AWS
                    {
                        if (uploadedFile.LastWriteTime == file.LastWriteTime.ToUnixTimestamp())
                        {
                            lock (_locker)
                            {
                                files.Add(uploadedFile);
                                ++counter;
                            }  
                            return; //do not uplad, file did not changed
                        }

                        if (file.MD5().ToHexString() == uploadedFile.MD5)
                        {
                            Console.WriteLine($"Skipping upload of '{file.FullName}', file with MD5 '{uploadedFile.MD5}' alredy exists in the '{bucket}/{destination}'.");
                            lock (_locker)
                            {
                                ++counter;
                                files.Add(uploadedFile);
                            }
                            return;
                        }
                    }

                    lock (_locker)
                    {
                        if (!isStatusFileUpdated) //update status file
                        {
                            status.timestamp = timestamp;
                            status.version = status.version + 1;
                            status.finalized = false;
                            var statusUploadResult = _S3Helper.UploadJsonAsync(status.bucket, status.key, status)
                                .Timeout(msTimeout: st.timeout)
                                .TryCatchRetryAsync(maxRepeats: st.retry).Result;

                            isStatusFileUpdated = true;
                        }

                        ++counter;
                        if (st.verbose >= 1) Console.WriteLine($"Uploading [{counter}/{sourceInfo.files.Length}][{file.Length}B] '{file.FullName}' => '{bucket}/{destination}' ...");
                    }

                    async Task<string> UploadFile()
                    {
                        file?.Refresh();
                        if (file == null || !file.Exists)
                            return null;

                        var hashExternal = file.MD5().ToHexString();
                        using (var fs = file.OpenRead()) //upload new file to AWS
                        {
                            var hash = await _S3Helper.UploadStreamAsync(bucketName: bucket,
                                 key: destination,
                                 inputStream: fs,
                                 throwIfAlreadyExists: false, msTimeout: st.timeout).TryCatchRetryAsync(maxRepeats: st.retry);

                            fs.Close();
                            return hash.IsNullOrEmpty() ? null : hash;
                        }
                    }

                    var md5 = await UploadFile().TryCatchRetryAsync(maxRepeats: st.retry);

                    if (!md5.IsNullOrEmpty())
                    {
                        lock (_locker)
                        {
                            files.Add(file.ToSilyFileInfo(md5));
                            var megabytes = (double)(file.Length + (md5.Length + bucket.Length + key.Length)*sizeof(char)) / (1024 * 1024);
                            var seconds = (double)(sw.ElapsedMilliseconds + 1) / 1000;
                            var speed = megabytes / seconds;
                            speedList.Add(speed);
                        }
                    }
                    else
                        Console.WriteLine($"FAILED, Upload '{file.FullName}' => '{bucket}/{destination}'");
                }
                finally
                {
                    _syncInfo[st.id].processed += file.Length;
                    _syncInfo[st.id].progress = ((double)_syncInfo[st.id].processed / _syncInfo[st.id].total) * 100;
                }
            }, maxDegreeOfParallelism: st.parallelism);

            var directories = sourceInfo.directories.Select(x => x.ToSilyDirectoryInfo()).ToArray();
            var cleanupResult = await cleanupTask;
            var avgSpeed = speedList.IsNullOrEmpty() ? double.NaN : speedList.Average();

            if (isStatusFileUpdated || //if modifications were made to files
                !status.directories.JsonEquals(directories)) // or directories
            {
                status.files = files.ToArray();
                status.finalized = true;
                status.counter += 1;
                status.directories = directories;
                status.source = st.source;
                status.destination = st.destination;
                var uploadResult = await _S3Helper.UploadJsonAsync(status.bucket, status.key, status)
                    .Timeout(msTimeout: st.timeout)
                    .TryCatchRetryAsync(maxRepeats: st.retry);

                Console.WriteLine($"SUCCESS, processed '{st.status}', all {status.files.Length} files and {status.directories.Length} directories were updated.");
                Console.WriteLine($"Average Upload Speed: {avgSpeed} MB/s");
            }
             
            return new SyncResult(success: true, speed: avgSpeed);
        }
    }
}
