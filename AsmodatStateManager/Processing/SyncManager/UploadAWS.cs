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
using System.Diagnostics;
using System.IO;

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
                var folderBKP = st.destination.ToBucketKeyPair();
                var result = await _S3Helper.DeleteObjectAsync(
                    bucketName: folderBKP.bucket, 
                    key: file, 
                    throwOnFailure: false, 
                    cancellationToken: cts.Token).TryCancelAfter(cts.Token, msTimeout: st.timeout);

                if(success)
                    Console.WriteLine($"Status file: '{folderBKP.bucket}/{file}' was removed.");
                else
                    Console.WriteLine($"Failed to remove status file: '{folderBKP.bucket}/{file}'.");

            }, maxDegreeOfParallelism: _cfg.parallelism);

            return success;
        }

        public async Task<SyncResult> UploadAWS(SyncTarget st)
        {
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
            var elspased = DateTimeEx.UnixTimestampNow() - status.timestamp;

            if (status.finalized)
            {
                var remaining = st.retention - elspased;
                Console.WriteLine($"Upload sync file '{st.status}' was already finalized {elspased}s ago. Next sync in {st.retention - elspased}s.");
                await Task.Delay(millisecondsDelay: 1000);
                return new SyncResult(success: true);
            }

            _syncInfo[st.id] = new SyncInfo(st);
            _syncInfo[st.id].total = sourceInfo.files.Sum(x => x?.Length ?? 0);
            _syncInfo[st.id].timestamp = timestamp;

            var cleanup = st.cleanup ? Cleanup(st, status) : null;
            var isStatusFileUpdated = false;
            var files = new List<SilyFileInfo>();
            var speedList = new List<double>();

            await ParallelEx.ForEachAsync(sourceInfo.files, async file =>
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    var uploadedFile = status.files?.FirstOrDefault(x => x.FullNameEqual(file));

                    string localMD5;
                    string destination;
                    if (uploadedFile != null) //file was already uploaded to AWS
                    {
                        if (uploadedFile.LastWriteTime == file.LastWriteTime.ToUnixTimestamp())
                        {
                            if(st.verbose > 1)
                                Console.WriteLine($"Skipping upload of '{file.FullName}', file did not changed since last upload.");

                            lock (_locker)
                            {
                                files.Add(uploadedFile);
                                ++counter;
                            }
                            return; //do not uplad, file did not changed
                        }

                        localMD5 = file.MD5().ToHexString();
                        destination = $"{key}/{localMD5}";
                        if (localMD5 == uploadedFile.MD5)
                        {
                            if (st.verbose > 1)
                                Console.WriteLine($"Skipping upload of '{file.FullName}', file alredy exists in the '{bucket}/{destination}'.");

                            lock (_locker)
                            {
                                ++counter;
                                files.Add(uploadedFile);
                            }
                            return;
                        }
                    }
                    else //file was not uploaded to AWS yet
                    {
                        localMD5 = file.MD5().ToHexString();
                        destination = $"{key}/{localMD5}";
                        if (await _S3Helper.ObjectExistsAsync(bucketName: bucket, key: $"{key}/{localMD5}")
                            .Timeout(msTimeout: st.timeout)
                            .TryCatchRetryAsync(maxRepeats: st.retry))
                        {
                            lock (_locker)
                            {
                                ++counter;
                                files.Add(file.ToSilyFileInfo(md5: localMD5));
                            }
                            if (st.verbose > 1)
                                Console.WriteLine($"Skipping upload of '{file.FullName}', file was found in the '{bucket}/{destination}'.");
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
                    }

                    async Task<string> UploadFile()
                    {
                        file?.Refresh();
                        if (file == null || !file.Exists)
                            return null;

                        using (var fs = File.Open( //upload new file to AWS
                            file.FullName, 
                            FileMode.Open, 
                            FileAccess.Read, 
                            EnumEx.ToEnum<FileShare>(st.filesShare)))
                        {
                            var hash = await _S3Helper.UploadStreamAsync(bucketName: bucket,
                                 key: destination,
                                 inputStream: fs,
                                 throwIfAlreadyExists: false, msTimeout: st.timeout).TryCatchRetryAsync(maxRepeats: st.retry);

                            fs.Close();
                            return hash.IsNullOrEmpty() ? null : hash;
                        }
                    }

                    Console.WriteLine($"Uploading [{counter}/{sourceInfo.files.Length}][{file.Length}B] '{file.FullName}' => '{bucket}/{destination}' ...");
                    var md5 = await UploadFile().TryCatchRetryAsync(maxRepeats: st.retry);

                    if(md5 != localMD5)
                        Console.WriteLine($"Warning! file changed during upload '{file.FullName}' => '{bucket}/{destination}'.");

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
            var avgSpeed = speedList.IsNullOrEmpty() ? double.NaN : speedList.Average();

            if (cleanup != null)
                await cleanup;

            if (isStatusFileUpdated || //if modifications were made to files
                !status.directories.JsonEquals(directories)) // or directories
            {
                status.files = files.ToArray();
                status.finalized = true;
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
