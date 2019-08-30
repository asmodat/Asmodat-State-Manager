using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using AsmodatStandard.Extensions;
using AsmodatStandard.Extensions.Collections;
using System.Linq;
using System.Runtime.Loader;

namespace AsmodatStateManager
{
    public class Program
    {
        public static void Main(string[] args)
        {
            AssemblyLoadContext.Default.Unloading += Default_Unloading;
            BuildWebHost(args).Run();
        }

        private static void Default_Unloading(AssemblyLoadContext obj)
        {
            Console.WriteLine("AsmodatStateManager was Stopped.");
        }

        private const int defaultPort = 8000;

        public static IWebHost BuildWebHost(string[] args)
        {
            var argPort = (args.FirstOrDefault() ?? defaultPort.ToString()).ToIntOrDefault(defaultPort);
            var port = Environment.GetEnvironmentVariable("PORT").ToIntOrDefault(argPort);

            Console.WriteLine($"Starting AsmodatStateManager Server on PORT {port}");

           return WebHost.CreateDefaultBuilder(args)
            .UseStartup<Startup>()
            .UseKestrel(options =>
            {
                options.Listen(IPAddress.Any, port);
            }).ConfigureLogging((context, logging) =>
            {
                logging.AddConsole();
                logging.AddDebug();
            })
            .Build();
        }
    }
}
