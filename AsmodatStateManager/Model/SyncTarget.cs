using Microsoft.AspNetCore.Mvc;
using System;
using AsmodatStandard.Extensions;
using AsmodatStandard.Extensions.Collections;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using AsmodatStandard.Extensions.IO;
using System.Collections.Generic;

namespace AsmodatStateManager.Model
{
    public static class SyncTargetEx
    {
        public static bool IsUpload(this SyncTarget.types type) =>
            type == SyncTarget.types.awsUpload;
        public static bool IsDownload(this SyncTarget.types type) =>
            type == SyncTarget.types.awsDownload;

        public static (FileInfo[] files, DirectoryInfo[] directories, DirectoryInfo rootDirectory) GetSourceInfo(this SyncTarget st)
        {
            var files = new List<FileInfo>();
            DirectoryInfo[] directories = null;
            DirectoryInfo rootDirectory = null;
            if (st.source.IsFile())
            {
                var file = st.source.ToFileInfo();
                files.Add(file);
                rootDirectory = file.Directory;
                directories = new DirectoryInfo[1] { rootDirectory };
            }
            else if (st.source.IsDirectory())
            {
                rootDirectory = st.source.ToDirectoryInfo();
                directories = rootDirectory.GetDirectories(recursive: st.recursive).Merge(rootDirectory);
                files = FileHelper.GetFiles(st.source, recursive: st.recursive).ToList();

                if (!st.filesIgnore.IsNullOrEmpty() && !files.IsNullOrEmpty()) //remove files that should be ignored
                    foreach (var ignore in st.filesIgnore)
                    {
                        if (ignore.IsNullOrEmpty())
                            continue;

                        if (files.IsNullOrEmpty())
                            break;

                        var remove = FileHelper.GetFiles(st.source, recursive: st.recursive, pattern: ignore);
                        foreach (var r in remove)
                        {
                            var toRemove = files.FirstOrDefault(x => x.FullName == r.FullName);
                            if (toRemove != null)
                                files.Remove(toRemove);

                            if (files.IsNullOrEmpty())
                                break;
                        }
                    }
            }
            else
            {
                var message = $"Could not process, source not found: '{st.source}'";
                if (st.throwIfSourceNotFound)
                    throw new Exception(message);
                else
                    Console.WriteLine(message);
            }

            return (files?.ToArray(), directories, rootDirectory);
        }
    }

    public class SyncTarget
    {
        public enum types : int
        {
            none = 0,
            awsUpload = 1,
            awsDownload = 2
        }

        public string id { get; set; }
        public types type { get; set; }
        public string description { get; set; }

        public string source { get; set; }
        public bool throwIfSourceNotFound { get; set; } = false;
        public string destination { get; set; }
        public string role { get; set; }
        public bool recursive { get; set; } = false;

        /// <summary>
        /// defines if files not on the sync list should be deleted
        /// </summary>
        public bool wipe { get; set; } = false;

        /// <summary>
        /// Used to verify MD5 checksum
        /// </summary>
        public bool verify { get; set; } = false;
        public int retry { get; set; } = 1;


        public int rotation { get; set; } = 3;

        /// <summary>
        /// seconds 
        /// </summary>
        public int retention { get; set; } = 0;

        /// <summary>
        /// Status file location
        /// </summary>
        public string status { get; set; }

        /// <summary>
        /// Time to retry operation in s
        /// </summary>
        public int intensity { get; set; }

        /// <summary>
        /// Maximum degree of parallelism
        /// </summary>
        public int parallelism { get; set; } = 1;

        /// <summary>
        /// AWS Profile name
        /// </summary>
        public string profile { get; set; }

        public int verbose { get; set; }

        /// <summary>
        /// Timestamp format: yyyyMMddHHmmssfff
        /// </summary>
        public long maxTimestamp { get; set; }

        /// <summary>
        /// Timestamp format: yyyyMMddHHmmssfff
        /// </summary>
        public long minTimestamp { get; set; }

        public int timeout { get; set; } = int.MaxValue;

        /// <summary>
        /// Max Sync Count
        /// 0 = infinite
        /// </summary>
        public int maxSyncCount { get; set; } = 0;

        /// <summary>
        /// Location of the sync target on the local machine.
        /// </summary>
        public string path { get; set; }

        public string[] filesIgnore { get; set; }

        /// <summary>
        /// Share mores: None, ReadWrite
        /// </summary>
        public string filesShare { get; set; } = "None";

        /// <summary>
        /// Defines how long in ms sync should sleep after completition
        /// </summary>
        public int sleep { get; set; } = 0;
    }
}
