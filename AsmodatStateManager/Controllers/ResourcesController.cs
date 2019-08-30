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

namespace AsmodatStateManager.Controllers
{
    [Authorize]
    [Route("api/resources")]
    public class ResourcesController : Controller
    {
        private readonly ManagerConfig _cfg;

        private PerformanceManager _pm;

        public ResourcesController(
            IHttpContextAccessor accessor,
            IHostingEnvironment hostingEnvironment,
            PerformanceManager pm,
            IOptions<ManagerConfig> cfg)
        {
            _pm = pm;
            _cfg = cfg.Value;
        }

        [AllowAnonymous]
        [HttpGet("health")]
        public IActionResult health()
        {
            if(_cfg.diskHealthChecks.IsNullOrEmpty())
                return StatusCode(StatusCodes.Status500InternalServerError, "Disk Health Checks configuraiton is not defined.");

            var driveStatus = _pm.GetDriveInfo().Where(x => _cfg.diskHealthChecks.Any(y => x.NameEquals(y.Key)));

            if (driveStatus.IsNullOrEmpty())
                return StatusCode(StatusCodes.Status500InternalServerError, "No Drive Info");


            var errorResponse = "";
            foreach(var ds in driveStatus)
            {
                var check = _cfg.diskHealthChecks.First(x => ds.NameEquals(x.Key)).Value;
                var used = 100 - ds.free;

                if (used > check)
                    errorResponse += $"Capacity of {ds.name} exceeded {check}% ({used}%).\n\r";
            }

            if(!errorResponse.IsNullOrEmpty())
                return StatusCode(StatusCodes.Status500InternalServerError, errorResponse);

            return StatusCode(StatusCodes.Status200OK, "healthy");
        }

        [HttpGet("disks")]
        public IActionResult disks(string name = null)
        {
            var results = _pm.GetDriveInfo();

            if (!name.IsNullOrEmpty())
                results = results.Where(x => x.NameEquals(name)).ToArray();

            if (results.IsNullOrEmpty())
                return StatusCode(StatusCodes.Status500InternalServerError, $"No info for drive '{(name.IsNullOrEmpty() ? name : "any")}'.");

            return StatusCode(StatusCodes.Status200OK, results);
        }
    }
}
