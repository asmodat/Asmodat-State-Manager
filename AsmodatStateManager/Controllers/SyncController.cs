using Microsoft.AspNetCore.Mvc;
using System;
using AsmodatStandard.Extensions;
using AsmodatStandard.Extensions.Collections;
using System.Threading.Tasks;
using AsmodatStateManager.Processing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using AsmodatStateManager.Model;
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using AsmodatStandard.Extensions.IO;
using AsmodatStandard.Cryptography;
using AsmodatStandard.Types;
using System.IO;
using System.Collections.Generic;

namespace AsmodatStateManager.Controllers
{
    [Authorize]
    [Route("api/sync")]
    public class SyncController : Controller
    {
        private readonly ManagerConfig _cfg;
        private SyncManager _sm;

        public SyncController(
            IHttpContextAccessor accessor,
            IHostingEnvironment hostingEnvironment,
            SyncManager sm,
            IOptions<ManagerConfig> cfg)
        {
            _sm = sm;
            _cfg = cfg.Value;
        }

        [AllowAnonymous]
        [HttpGet("health")]
        public IActionResult health()
        {
            var status = _sm.GetSyncStatus();

            if (status == null || status.results.IsNullOrEmpty())
                return StatusCode(StatusCodes.Status500InternalServerError, "Failure, Status or Sync results were NOT found");

            if(status.results.All(x => x.Value?.success == true))
                return StatusCode(StatusCodes.Status200OK, "healthy");

            return StatusCode(StatusCodes.Status500InternalServerError, status.results.Where(x => x.Value?.success != true).Select(x => x.Key).ToArray());
        }

        [HttpGet("status")]
        public IActionResult status()
        {
            var results = _sm.GetSyncStatus();

            if (results == null)
                return StatusCode(StatusCodes.Status500InternalServerError, "Status Not Found");

            return StatusCode(StatusCodes.Status200OK, results);
        }

        [HttpGet("targets")]
        public IActionResult targets(string id = null)
        {
            var results = _cfg.GetSyncTargets();

            if (!id.IsNullOrEmpty())
                id = id.Trim('"', '\\','/', ' ');

            if (!id.IsNullOrEmpty() && !results.IsNullOrEmpty())
                results = results.Where(x => x.id == id).ToArray();

            if (results.IsNullOrEmpty())
                return StatusCode(StatusCodes.Status500InternalServerError, $"No Sync Targets Were Found in '{_cfg.targets ?? "undefined"}' with id's '{(id.IsNullOrEmpty() ? "any" : id.JsonSerialize() )}'.");

            return StatusCode(StatusCodes.Status200OK, results);
        }

        [HttpPut("add")]
        public IActionResult add([FromBody]SyncTarget syncTarget)
        {
            if (syncTarget == null || syncTarget.id.IsNullOrEmpty())
                return StatusCode(StatusCodes.Status500InternalServerError, "Sync Target or it's id was not defined.");

            var file = PathEx.RuntimeCombine(_cfg.targets, $"sync-target-{syncTarget.id}.json").ToFileInfo();

            file.WriteAllText(syncTarget.JsonSerialize());
            file.Refresh();

            if(!file.Exists || file.Length == 0)
                return StatusCode(StatusCodes.Status500InternalServerError, $"Failed to create Sync Target '{syncTarget.id}'.");

            return StatusCode(StatusCodes.Status200OK, file.ToSilyFileInfo());
        }

        [HttpDelete("delete")]
        public IActionResult delete(string id)
        {
            if (id.IsNullOrEmpty())
                return StatusCode(StatusCodes.Status500InternalServerError, "Sync Target was not defined.");

            var results = _cfg.GetSyncTargets();

            if (results.IsNullOrEmpty())
                return StatusCode(StatusCodes.Status200OK, "All Sync Targets were already removed.");

            var removed = new List<FileInfo>();
            foreach(var st in results)
            {
                var info = st.path.ToFileInfo();
                if (st.id != id)
                    continue;

                info.TryDelete();
                info.Refresh();

                if(info.Exists)
                    return StatusCode(StatusCodes.Status500InternalServerError, $"Failed to remove Sync Info ({id}) file '{st?.path ?? "undefined"}'.");

                removed.Add(info);
            }

            var success = removed.IsNullOrEmpty() || removed.All(x => !x.Exists);
            return StatusCode(StatusCodes.Status200OK, $"Success all ({removed.Count}) Sync Files with id '{id}' were removed.");
        }
    }
}
