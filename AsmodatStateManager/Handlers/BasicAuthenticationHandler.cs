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
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using System.Text.Encodings.Web;
using AsmodatStandard.Extensions.AspNetCore;
using System.Security.Claims;

namespace AsmodatStateManager.Handlers
{
    public class BasicAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly ManagerConfig _cfg;

        public BasicAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock,
            IOptions<ManagerConfig> cfg)
            : base(options, logger, encoder, clock)
        {
            _cfg = cfg.Value;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            //TODO: IMPLEMENT RATE LIMITTING
            await Task.Delay(10);

            if (!Request.Headers.ContainsKey("Authorization"))
                return AuthenticateResult.Fail("Missing Authorization Header");

            if (Request == null || _cfg == null)
                return AuthenticateResult.Fail("Invalid Request");

            (string login, string password) credentials;

            try
            {
                credentials = Request.GetBasicAuthCredentials();
            }
            catch
            {
                return AuthenticateResult.Fail("Failed To Read Basic Auth Credentials");
            }

            var isAuthorized = !credentials.login.IsNullOrEmpty() &&
                !credentials.password.IsNullOrEmpty() &&
                !_cfg.login.IsNullOrEmpty() &&
                !_cfg.password.IsNullOrEmpty() &&
                credentials.login == _cfg.login &&
                credentials.password == _cfg.password;

            if (isAuthorized)
            {
                var claims = new[] {
                    new Claim(ClaimTypes.Name, _cfg.login),
                };

                var identity = new ClaimsIdentity(claims, Scheme.Name);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);
                return AuthenticateResult.Success(ticket);
            }

            return AuthenticateResult.Fail("Request is not authorized.");
        }
    }
}
