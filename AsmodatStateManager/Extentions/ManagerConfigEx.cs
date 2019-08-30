using AsmodatStandard.Extensions.AspNetCore;
using AsmodatStateManager.Model;
using AsmodatStateManager.Processing;
using Microsoft.AspNetCore.Http;

namespace AsmodatStateManager.Ententions
{
    public static class ManagerConfigEx
    {
        public static bool IsAuthorized(this HttpRequest request, ManagerConfig mc)
        {
            if (request == null || mc == null)
                return false;

            var credentials = request.GetBasicAuthCredentials();

            return
                credentials.login != null && mc.login != null &&
                credentials.login == mc.login &&
                credentials.password == mc.password;
        }
    }
}
